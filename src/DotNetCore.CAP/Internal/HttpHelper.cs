using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using DotNetCore.CAP.Models;

namespace DotNetCore.CAP.Internal
{
    public class HttpHelper
    {
        public static IEnumerable<CapWebMessage> WebMessages = null;
        public static DateTime LastRefreshTime = DateTime.MinValue;
        private IStorage _storage;

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="storage"></param>
        public HttpHelper(IStorage storage)
        {
            this._storage = storage;
        }

        /// <summary>
        /// 当前消息
        /// </summary>
        public MessageContext DeliverMessage { get; set; }


        /// <summary>
        /// 网络消息消费时处理方式
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public async Task<string> SendAsync(string content)
        {
            if (DateTime.Now > LastRefreshTime.AddMinutes(1))
            {
                LastRefreshTime = DateTime.Now;
                WebMessages = null;
            }
            var _headers = new Dictionary<string, string>();
            var _current = WebMessages?.FirstOrDefault(f1 => f1.Group == DeliverMessage.Group && f1.Name == DeliverMessage.Name);
            if (_current == null)
            {
                WebMessages = _storage.GetConnection().GetWebMessages().GetAwaiter().GetResult();
                _current = WebMessages?.FirstOrDefault(f1 => f1.Group == DeliverMessage.Group && f1.Name == DeliverMessage.Name);
            }
            if (_current == null) { throw new Exception("无相关相关消息消费配置！"); }
            return await SendAsync(_current.Url, _current.Method, _current.Headers, content);
        }

        /// <summary>
        /// 获取请求数据内容
        /// </summary>
        /// <param name="requestUri"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        private async Task<string> SendAsync(string requestUri, string method, string headers, string content)
        {
            var _headers = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(headers))
            {
                foreach (var item in headers.Split(System.Environment.NewLine.ToCharArray()))
                {
                    if (item.Contains(":"))
                    {
                        var key = item.Split(':')[0];
                        _headers.Add(key, item.Substring(key.Length + 1));
                    }
                }
            }
            Console.WriteLine($"执行网络请求: [{method}]{requestUri}");
            return await SendAsync(requestUri, new HttpMethod(method), _headers, content);
        }


        /// <summary>
        /// 执行网络请求
        /// </summary>
        /// <param name="requestUri">请求地址</param>
        /// <param name="method">请求方式</param>
        /// <param name="headers">请求头</param>
        /// <param name="content">请求内容</param>
        /// <returns></returns>
        private async Task<string> SendAsync(string requestUri, HttpMethod method, Dictionary<string, string> headers, string content)
        {
            using (MemoryStream ms = new MemoryStream())
            using (HttpClient client = new HttpClient(new HttpClientHandler() { UseCookies=false }))
            {
                byte[] bytes = string.IsNullOrEmpty(content) ? new byte[0] : Encoding.UTF8.GetBytes(content);
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);
                HttpContent hc = new StreamContent(ms);
                client.Timeout = TimeSpan.FromSeconds(20);
                HttpRequestMessage request = new HttpRequestMessage(method, requestUri);
                request.Content = hc;
                foreach (var item in headers)
                {
                    switch (item.Key.ToLower())
                    {
                        //case "host":
                        //    request.Headers.Host = item.Value;
                        //    break;
                        //case "referrer":
                        //    request.Headers.Referrer = new Uri(item.Value);
                        //    break;
                        case "useragent":
                            request.Headers.UserAgent.Clear();
                            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(item.Value));
                            break;
                        case "authorization":
                            request.Headers.Authorization = new AuthenticationHeaderValue(item.Value);
                            break;
                        case "accept":
                            request.Headers.Accept.Clear();
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(item.Value));
                            break;
                        case "acceptcharset":
                            request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue(item.Value));
                            break;
                        case "acceptencoding":
                            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(item.Value));
                            break;
                        case "acceptlanguage":
                            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(item.Value));
                            break;
                        case "cachecontrol":
                            request.Headers.CacheControl = new CacheControlHeaderValue();
                            break;
                        //case "from":
                        //    request.Headers.From = item.Value;
                        //    break;
                        default:
                            request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                            break;
                    }
                }
                HttpResponseMessage resp = await client.SendAsync(request);
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }
    }
}
