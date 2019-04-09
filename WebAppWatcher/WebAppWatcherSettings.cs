using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebAppWatcher
{
    public class WebAppWatcherSettings
    {
       
        public List<WebApiEntity> WebApis
        {
            get; set;
        }
        public string WatchCommand { get; set; }

        private string _logAnalyticsWorkspaceId;

        public string LogAnalyticsWorkspaceId
        {
            get
            {
                if (_logAnalyticsWorkspaceId == null || _logAnalyticsWorkspaceId == string.Empty)
                {
                    _logAnalyticsWorkspaceId = Environment.GetEnvironmentVariable("LogAnalyticsWorkspaceId");
                }
                return _logAnalyticsWorkspaceId;
            }
        }

        private string _logAnalyticsWorkspaceKey;

        public string LogAnalyticsWorkspaceKey
        {
            get
            {
                if (_logAnalyticsWorkspaceKey == null || _logAnalyticsWorkspaceKey == string.Empty)
                {
                    _logAnalyticsWorkspaceKey = Environment.GetEnvironmentVariable("LogAnalyticsWorkspaceKey");
                }
                return _logAnalyticsWorkspaceKey;
            }
        }

        public string LogName { get; set; }
        public string LogAnalyticsUrlFormat { get; set; }
        
        private List<WebApiCookie> customCookies;

       public List<WebApiCookie> CustomCookies { get {
                if (customCookies == null)
                {
                    customCookies = JsonConvert.DeserializeObject<List<WebApiCookie>>(Environment.GetEnvironmentVariable("Cookies"));
                }

                return customCookies;
            }
        }
        
    }
    public class WebApiEntity
    {
        public string ResourceId { get; set; }
        public string Url { get; set; }
        public bool IsSoap { get; set; }
        public bool IsDown { get; set; }
        
    }

    public class WebApiCookie
    {
        public string name { get; set; }
        public string value { get; set; }
    }
}
