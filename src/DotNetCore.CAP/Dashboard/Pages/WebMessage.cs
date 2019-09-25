using System.Collections.Generic;
using DotNetCore.CAP.Models;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCore.CAP.Dashboard.Pages
{
    internal partial class WebMessage
    {
        private IStorage _storage = null;
        private IEnumerable<CapWebMessage> _webmessages;


        /// <summary>
        /// 获得总量
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        public int GetTotal(IMonitoringApi api)
        {
            return api.WebMessageCount();
        }


        /// <summary>
        /// 获取当前所有消息信息
        /// </summary>
        public IEnumerable<CapWebMessage> WebMessages
        {
            get
            {
                if (_webmessages == null)
                {
                    _storage = RequestServices.GetService<IStorage>();
                    if (_storage == null)
                    {
                        return new List<CapWebMessage>();
                    }
                    _webmessages = _storage.GetConnection().GetWebMessages().GetAwaiter().GetResult();
                }
                return _webmessages;
            }
        }




    }
}
