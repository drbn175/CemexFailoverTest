using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionWatcher
{
    class SqlTransactionWatcherSettings
    {
        public List<DataBaseTransactionEntity> DataBases
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
    public class DataBaseTransactionEntity
    {
        public string ResourceId { get; set; }
        public string DataBaseConnection { get; set; }
        public bool IsBlocked { get; set; }

        public List<Dictionary<string, object>> Records;

    }

}
