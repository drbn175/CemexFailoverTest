using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CemexDataAcces;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace SqlTransactionWatcher
{
    public static class SqlTransactionWatcher
    {
        [FunctionName("SqlTransactionWatcher")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            try
            {
                SqlTransactionWatcherSettings SqlTransactionWatcherSettings = context.GetInput<SqlTransactionWatcherSettings>();

                if (SqlTransactionWatcherSettings == null)
                {
                    SqlTransactionWatcherSettings = new SqlTransactionWatcherSettings();
                }
                if (SqlTransactionWatcherSettings.DataBases == null)
                {
                    SqlTransactionWatcherSettings.DataBases = JsonConvert.DeserializeObject<List<DataBaseTransactionEntity>>(Environment.GetEnvironmentVariable("DataBases"));
                }
                //Log Analytics 
                string logName = Environment.GetEnvironmentVariable("AzureFunctionLog");
                if (logName == null || logName == string.Empty)
                {
                    throw new Exception($"Setting configuration error! AzureFunctionLog");
                }
                string logAnalyticsUrlFormat = Environment.GetEnvironmentVariable("LogAnalyticsUrl");
                if (logAnalyticsUrlFormat == null || logAnalyticsUrlFormat == string.Empty)
                {
                    throw new Exception($"Setting configuration error! LogAnalyticsUrl");
                }
                foreach (DataBaseTransactionEntity db in SqlTransactionWatcherSettings.DataBases)
                {
                    IEnumerable<Dictionary<string, object>> blocked = await context.CallActivityAsync<IEnumerable<Dictionary<string, object>>>("SqlTransactionWatcher_Check", db);
                    if(blocked.Count() > 0)
                    {
                        db.IsBlocked = true; 
                       foreach(Dictionary<string,object> record in blocked)
                        {
                            record.Add("ResourceId", db.ResourceId);
                            record.Add("DataBaseConnection", db.DataBaseConnection);
                            record.Add("IsBlocked", db.IsBlocked);
                        }
                        await LogAnalyticsHelper.LogAnalyticsHelper.LogDataAsync(SqlTransactionWatcherSettings.LogAnalyticsWorkspaceId, logAnalyticsUrlFormat, SqlTransactionWatcherSettings.LogAnalyticsWorkspaceKey, logName, blocked.ToList());
                    }
                }
                int nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("Schedule"));
                DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(nextCleanUpSeconds);
                await context.CreateTimer(nextCleanup, CancellationToken.None);
                context.ContinueAsNew(SqlTransactionWatcherSettings);
            }
            catch(Exception ex)
            {
                context.SetCustomStatus($"Setting configuration error! { ex.Message } { ex.StackTrace }");
            }
        }

        [FunctionName("SqlTransactionWatcher_Check")]
        public static IEnumerable<Dictionary<string, object>> DoCheck([ActivityTrigger] DataBaseTransactionEntity db, ILogger log)
        {
            IEnumerable<Dictionary<string, object>> result;
            int retryCount;
            int initialInterval;
            int increment;
            int databaseTimeout;
            int seconds;
            string command;
            try
            {
                retryCount = int.Parse(Environment.GetEnvironmentVariable("RetryCount"));
                initialInterval = int.Parse(Environment.GetEnvironmentVariable("InitialInterval"));
                increment = int.Parse(Environment.GetEnvironmentVariable("Increment"));
                databaseTimeout = int.Parse(Environment.GetEnvironmentVariable("DatabaseTimeout"));
                seconds = int.Parse(Environment.GetEnvironmentVariable("Seconds"));
                command = Environment.GetEnvironmentVariable("WatchCommand");
                if (db != null && db.ResourceId != string.Empty)
                {
                    log.LogInformation($"Access to DB {db.ResourceId}.");
                    SQLDataAccess sqlCheck = new SQLDataAccess(db.DataBaseConnection, retryCount, initialInterval, increment, databaseTimeout);
                    result = JsonConvert.DeserializeObject<IEnumerable<Dictionary<string, object>>>(sqlCheck.ExecuteReader(string.Format(command, seconds), null));
                }
                else
                {
                    result = null;
                }
            }
            catch (Exception ex)
            {
                result = null;
            }
            log.LogInformation($"SQL Check Result { result }");
            return result;
        }

      

        [FunctionName("SqlTransactionWatcher_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("SqlTransactionWatcher", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}