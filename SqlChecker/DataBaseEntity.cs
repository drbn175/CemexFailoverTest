using System;
using System.Collections.Generic;
using System.Text;

namespace SqlChecker
{
    public class SqlCheckerSettings
    {
        public  List<DataBaseEntity> DataBases
        {
            get;set;
        }

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
    public class DataBaseEntity
    {
        public string ResourceId { get; set; }
        public string PrincipalFailoverGroup { get; set; }
        public string SecondaryFailoverGroup { get; set; }
        public string DataBaseConnection { get; set; }
        public bool IsAlive { get; set; }
        public int Retries { get; set; }
        public string Command { get; set; }
        public CustomException Exception { get; set; }
    }
    public class CustomException
    {
        public string Message { get; set; }
        public string StackTrace { get; set; }
    }
}
