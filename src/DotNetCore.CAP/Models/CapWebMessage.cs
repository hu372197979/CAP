// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace DotNetCore.CAP.Models
{

    /// <summary>
    /// 网络消息处理
    /// </summary>
    public class CapWebMessage
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CapWebMessage" />.
        /// </summary>
        public CapWebMessage()
        {
            Added = DateTime.Now;
            Edited = DateTime.Now;
        }

        public CapWebMessage(MessageContext message) : this()
        {
            Group = message.Group;
            Name = message.Name;
            Content = message.Content;
        }

        public long Id { get; set; }

        public string IdString
        {
            get;
            set;
        }

        public string Group { get; set; }

        public string Name { get; set; }

        public string Content { get; set; }

        public DateTime Added { get; set; }

        public DateTime? Edited { get; set; }

        public string Url { get; set; }

        public string Method { get; set; }

        public string Headers { get; set; }

        public MessageContext ToMessageContext()
        {
            return new MessageContext
            {
                Group = Group,
                Name = Name,
                Content = Content
            };
        }

        public override string ToString()
        {
            return "name:" + Name + ", group:" + Group + ", content:" + Content;
        }
        public string ToJsonString()
        {
            this.IdString = this.Id.ToString();
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
    }
}