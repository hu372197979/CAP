// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DotNetCore.CAP.Abstractions;
using DotNetCore.CAP.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace DotNetCore.CAP
{
    /// <inheritdoc />
    /// <summary>
    /// A default <see cref="T:DotNetCore.CAP.Abstractions.IConsumerServiceSelector" /> implementation.
    /// </summary>
    public class DefaultConsumerServiceSelector : IConsumerServiceSelector
    {
        private readonly CapOptions _capOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly IStorage _storage;

        /// <summary>
        /// since this class be designed as a Singleton service,the following two list must be thread safe!
        /// </summary>
        private readonly ConcurrentDictionary<string, List<RegexExecuteDescriptor<ConsumerExecutorDescriptor>>> _asteriskList;
        private readonly ConcurrentDictionary<string, List<RegexExecuteDescriptor<ConsumerExecutorDescriptor>>> _poundList;

        /// <summary>
        /// Creates a new <see cref="DefaultConsumerServiceSelector" />.
        /// </summary>
        public DefaultConsumerServiceSelector(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _capOptions = serviceProvider.GetService<IOptions<CapOptions>>().Value;
            _storage = serviceProvider.GetService<IStorage>();

            _asteriskList = new ConcurrentDictionary<string, List<RegexExecuteDescriptor<ConsumerExecutorDescriptor>>>();
            _poundList = new ConcurrentDictionary<string, List<RegexExecuteDescriptor<ConsumerExecutorDescriptor>>>();
        }

        public IReadOnlyList<ConsumerExecutorDescriptor> SelectCandidates()
        {
            var executorDescriptorList = new List<ConsumerExecutorDescriptor>();

            executorDescriptorList.AddRange(FindConsumersFromInterfaceTypes(_serviceProvider));

            executorDescriptorList.AddRange(FindConsumersFromControllerTypes());

            //加载通过配置增加的消费订阅
            executorDescriptorList.AddRange(FindWebMessageFromDB());

            return executorDescriptorList;
        }

        public ConsumerExecutorDescriptor SelectBestCandidate(string key, IReadOnlyList<ConsumerExecutorDescriptor> executeDescriptor)
        {
            var result = MatchUsingName(key, executeDescriptor);
            if (result != null)
            {
                return result;
            }

            //[*] match with regex, i.e.  foo.*.abc
            result = MatchAsteriskUsingRegex(key, executeDescriptor);
            if (result != null)
            {
                return result;
            }

            //[#] match regex, i.e. foo.#
            result = MatchPoundUsingRegex(key, executeDescriptor);
            return result;
        }

        protected virtual IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromInterfaceTypes(
            IServiceProvider provider)
        {
            var executorDescriptorList = new List<ConsumerExecutorDescriptor>();

            var capSubscribeTypeInfo = typeof(ICapSubscribe).GetTypeInfo();

            foreach (var service in ServiceCollectionExtensions.ServiceCollection.Where(o => o.ImplementationType != null && o.ServiceType != null))
            {
                var typeInfo = service.ImplementationType.GetTypeInfo();
                if (!capSubscribeTypeInfo.IsAssignableFrom(typeInfo))
                {
                    continue;
                }
                var serviceTypeInfo = service.ServiceType.GetTypeInfo();

                executorDescriptorList.AddRange(GetTopicAttributesDescription(typeInfo, serviceTypeInfo));
            }

            return executorDescriptorList;
        }

        protected virtual IEnumerable<ConsumerExecutorDescriptor> FindConsumersFromControllerTypes()
        {
            var executorDescriptorList = new List<ConsumerExecutorDescriptor>();

            var types = Assembly.GetEntryAssembly().ExportedTypes;
            foreach (var type in types)
            {
                var typeInfo = type.GetTypeInfo();
                if (Helper.IsController(typeInfo))
                {
                    executorDescriptorList.AddRange(GetTopicAttributesDescription(typeInfo));
                }
            }

            return executorDescriptorList;
        }

        /// <summary>
        /// 从存储位置获取所有网络消费接口
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<ConsumerExecutorDescriptor> FindWebMessageFromDB()
        {
            var ret = new List<ConsumerExecutorDescriptor>();
            var webMessages = _storage.GetConnection().GetWebMessages().GetAwaiter().GetResult();
            foreach (var item in webMessages)
            {
                ret.Add(new ConsumerExecutorDescriptor()
                {
                    Attribute = new CapSubscribeAttribute(item.Name) { Group = item.Group },
                    ImplTypeInfo = typeof(Internal.HttpHelper).GetTypeInfo(),
                    MethodInfo = typeof(Internal.HttpHelper).GetTypeInfo().DeclaredMethods.FirstOrDefault(x => x.IsPublic & x.Name == "SendAsync"),
                    ServiceTypeInfo = typeof(Internal.HttpHelper).GetTypeInfo()
                });
            }
            return ret;
        }


        protected IEnumerable<ConsumerExecutorDescriptor> GetTopicAttributesDescription(TypeInfo typeInfo, TypeInfo serviceTypeInfo = null)
        {
            foreach (var method in typeInfo.DeclaredMethods)
            {
                var topicAttr = method.GetCustomAttributes<TopicAttribute>(true);
                var topicAttributes = topicAttr as IList<TopicAttribute> ?? topicAttr.ToList();

                if (!topicAttributes.Any())
                {
                    continue;
                }

                foreach (var attr in topicAttributes)
                {
                    if (attr.Group == null)
                    {
                        attr.Group = _capOptions.DefaultGroup + "." + _capOptions.Version;
                    }
                    else
                    {
                        attr.Group = attr.Group + "." + _capOptions.Version;
                    }

                    yield return InitDescriptor(attr, method, typeInfo, serviceTypeInfo);
                }
            }
        }

        private static ConsumerExecutorDescriptor InitDescriptor(
            TopicAttribute attr,
            MethodInfo methodInfo,
            TypeInfo implType,
            TypeInfo serviceTypeInfo)
        {
            var descriptor = new ConsumerExecutorDescriptor
            {
                Attribute = attr,
                MethodInfo = methodInfo,
                ImplTypeInfo = implType,
                ServiceTypeInfo = serviceTypeInfo
            };

            return descriptor;
        }

        private ConsumerExecutorDescriptor MatchUsingName(string key, IReadOnlyList<ConsumerExecutorDescriptor> executeDescriptor)
        {
            return executeDescriptor.FirstOrDefault(x => x.Attribute.Name == key);
        }

        private ConsumerExecutorDescriptor MatchAsteriskUsingRegex(string key, IReadOnlyList<ConsumerExecutorDescriptor> executeDescriptor)
        {
            var group = executeDescriptor.First().Attribute.Group;
            if (!_asteriskList.TryGetValue(group, out var tmpList))
            {
                tmpList = executeDescriptor.Where(x => x.Attribute.Name.IndexOf('*') >= 0)
                    .Select(x => new RegexExecuteDescriptor<ConsumerExecutorDescriptor>
                    {
                        Name = ("^" + x.Attribute.Name + "$").Replace("*", "[0-9_a-zA-Z]+").Replace(".", "\\."),
                        Descriptor = x
                    }).ToList();
                _asteriskList.TryAdd(group, tmpList);
            }

            foreach (var red in tmpList)
            {
                if (Regex.IsMatch(key, red.Name, RegexOptions.Singleline))
                {
                    return red.Descriptor;
                }
            }

            return null;
        }

        private ConsumerExecutorDescriptor MatchPoundUsingRegex(string key, IReadOnlyList<ConsumerExecutorDescriptor> executeDescriptor)
        {
            var group = executeDescriptor.First().Attribute.Group;
            if (!_poundList.TryGetValue(group, out var tmpList))
            {
                tmpList = executeDescriptor
                    .Where(x => x.Attribute.Name.IndexOf('#') >= 0)
                    .Select(x => new RegexExecuteDescriptor<ConsumerExecutorDescriptor>
                    {
                        Name = ("^" + x.Attribute.Name.Replace(".", "\\.") + "$").Replace("#", "[0-9_a-zA-Z\\.]+"),
                        Descriptor = x
                    }).ToList();
                _poundList.TryAdd(group, tmpList);
            }

            foreach (var red in tmpList)
            {
                if (Regex.IsMatch(key, red.Name, RegexOptions.Singleline))
                {
                    return red.Descriptor;
                }
            }

            return null;
        }


        private class RegexExecuteDescriptor<T>
        {
            public string Name { get; set; }

            public T Descriptor { get; set; }
        }
    }
}
