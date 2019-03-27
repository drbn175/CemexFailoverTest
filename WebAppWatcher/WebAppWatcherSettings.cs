using System;
using System.Collections.Generic;
using System.Text;

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
    }
    public class WebApiEntity
    {
        public string ResourceId { get; set; }
        public string Url { get; set; }
        public bool IsDown { get; set; }
        
    }
}
