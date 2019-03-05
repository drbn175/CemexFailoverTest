using System;
using System.Collections.Generic;
using System.Text;

namespace SqlChecker
{
    public class DataBaseEntity
    {
        public string ResourceId { get; set; }
        public string DBServerFailoverTarget { get; set; }
        public string DataBaseConnection { get; set; }
        public List<string> WebAppsRelated { get; set; }
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
