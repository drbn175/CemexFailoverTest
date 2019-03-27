using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CemexDataAcces;

namespace SqlChecker
{
    public static class SqlChecker
    {
        [FunctionName("SqlChecker")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            try
            {
                SqlCheckerSettings sqlCheckerSettings = context.GetInput<SqlCheckerSettings>();
                int nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("Schedule"));
                if (sqlCheckerSettings == null)
                {
                    sqlCheckerSettings = new SqlCheckerSettings();
                }
                if (sqlCheckerSettings.DataBases == null)
                {
                    sqlCheckerSettings.DataBases = JsonConvert.DeserializeObject<List<DataBaseEntity>>(Environment.GetEnvironmentVariable("DataBases"));
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

                foreach (DataBaseEntity db in sqlCheckerSettings.DataBases)
                {
                    Tuple<bool, string, string> IsAlive = await context.CallActivityAsync<Tuple<bool, string, string>>("SqlChecker_Query", db);
                    db.IsAlive = IsAlive.Item1;
                    db.Exception = new CustomException() { Message = IsAlive.Item2, StackTrace = IsAlive.Item3 };
                    if (db.IsAlive)
                    {
                        db.Retries = 0;
                    }
                    else
                    {
                        db.Retries = db.Retries + 1;
                    }

                    await LogAnalyticsHelper.LogAnalyticsHelper.LogDataAsync(sqlCheckerSettings.LogAnalyticsWorkspaceId, logAnalyticsUrlFormat, sqlCheckerSettings.LogAnalyticsWorkspaceKey, logName, db);

                }
                if (sqlCheckerSettings.DataBases.Exists(item => !item.IsAlive))
                {
                    nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("CriticalSchedule"));
                }
                DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(nextCleanUpSeconds);
                await context.CreateTimer(nextCleanup, CancellationToken.None);
                context.ContinueAsNew(sqlCheckerSettings);
            }
            catch(Exception ex)
            {
                context.SetCustomStatus($"Setting configuration error! { ex.Message } { ex.StackTrace }");
            }
            
        }

        [FunctionName("SqlChecker_Query")]
        public static Tuple<bool, string,string> DoQuery([ActivityTrigger] DataBaseEntity db, ILogger log)
        {
            Tuple<bool, string, string> result = new Tuple<bool, string, string>(false, string.Empty, string.Empty);
            int retryCount;
            int initialInterval;
            int increment;
            int databaseTimeout;
            try
            {
                retryCount = int.Parse(Environment.GetEnvironmentVariable("RetryCount"));
                initialInterval = int.Parse(Environment.GetEnvironmentVariable("InitialInterval"));
                increment = int.Parse(Environment.GetEnvironmentVariable("Increment"));
                databaseTimeout = int.Parse(Environment.GetEnvironmentVariable("DatabaseTimeout"));
                if (db != null && db.ResourceId != string.Empty)
                {
                    log.LogInformation($"Access to DB {db.ResourceId}.");
                    if (db.DataBaseConnection == string.Empty || db.Command == string.Empty)
                    {
                        result = new Tuple<bool, string, string>(false, "Setting configuration error", "Check databases JSON");
                    }
                    else
                    {
                        SQLDataAccess sqlCheck = new SQLDataAccess(db.DataBaseConnection, retryCount, initialInterval, increment, databaseTimeout);
                        result = sqlCheck.ExecuteReaderPolicy(db.Command, null);
                    }
                }
                else
                {
                    result = new Tuple<bool, string, string>(false, "Setting configuration error", "Check databases JSON");
                }
            }catch(Exception ex)
            {
                result = new Tuple<bool, string, string>(false, ex.Message, ex.StackTrace);
            }
            log.LogInformation($"SQL Check Result { result.Item1} {result.Item2}. {result.Item3}");
            return result;
        }

       
        [FunctionName("SqlChecker_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("SqlChecker", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

       
    }
}