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
                if (sqlCheckerSettings == null)
                {
                    sqlCheckerSettings = new SqlCheckerSettings();
                }
                int nextCleanUpSeconds;
                int.TryParse(Environment.GetEnvironmentVariable("Schedule"), out nextCleanUpSeconds);
               
                Tuple<int, SqlCheckerSettings> result = await context.CallActivityAsync<Tuple<int, SqlCheckerSettings>>("SqlChecker_Query", sqlCheckerSettings);
                DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(result.Item1);
                await context.CreateTimer(nextCleanup, CancellationToken.None);
                context.ContinueAsNew(result.Item2);
            }
            catch(Exception ex)
            {
                context.SetCustomStatus($"Setting configuration error! { ex.Message } { ex.StackTrace }");
            }
            
        }

        [FunctionName("SqlChecker_Query")]
        public static async Task<Tuple<int, SqlCheckerSettings>> DoQueryAsync([ActivityTrigger] SqlCheckerSettings sqlCheckerSettings, ILogger log)
        {
           
            Tuple<bool, string, string> result = new Tuple<bool, string, string>(false, string.Empty, string.Empty);
            int nextCleanUpSeconds;
            int.TryParse(Environment.GetEnvironmentVariable("Schedule"),out nextCleanUpSeconds);
            int retryCount;
            int.TryParse(Environment.GetEnvironmentVariable("RetryCount"), out retryCount);
            int initialInterval;
            int.TryParse(Environment.GetEnvironmentVariable("InitialInterval"),out initialInterval);
            int increment;
            int.TryParse(Environment.GetEnvironmentVariable("Increment"), out increment);
            int databaseTimeout;
            int.TryParse(Environment.GetEnvironmentVariable("DatabaseTimeout"), out databaseTimeout);
            //Log Analytics 
            string logName = Environment.GetEnvironmentVariable("AzureFunctionLog");
            if (logName == null || logName == string.Empty)
            {
                log.LogInformation($"Setting configuration error! AzureFunctionLog");
                throw new Exception($"Setting configuration error! AzureFunctionLog");
            }
            string logAnalyticsUrlFormat = Environment.GetEnvironmentVariable("LogAnalyticsUrl");
            if (logAnalyticsUrlFormat == null || logAnalyticsUrlFormat == string.Empty)
            {
                log.LogInformation($"Setting configuration error! LogAnalyticsUrl");
                throw new Exception($"Setting configuration error! LogAnalyticsUrl");
            }
            SQLDataAccess sqlCheck = new SQLDataAccess(sqlCheckerSettings.ConnectionString, retryCount, initialInterval, increment, databaseTimeout);
            var resultDB = JsonConvert.DeserializeObject<IEnumerable<Dictionary<string, object>>>(sqlCheck.ExecuteReader(string.Format(Environment.GetEnvironmentVariable("Command"), Environment.GetEnvironmentVariable("Region"), Environment.GetEnvironmentVariable("Enviroment")), null));
            if(sqlCheckerSettings.DataBases == null)
            {
                sqlCheckerSettings.DataBases = new List<DataBaseEntity>();
                foreach (Dictionary<string, object> item in resultDB)
                {
                    if (item.ContainsKey("KVSecret1"))
                    {
                        Tuple<bool, string> key = await KeyVaultHelper.KeyVaultHelper.GetValueAsync(string.Format(Environment.GetEnvironmentVariable("KeyVaultUrl"), Environment.GetEnvironmentVariable("KeyVault")), item.GetValueOrDefault("KVSecret1").ToString(), Environment.GetEnvironmentVariable("ClientID"), Environment.GetEnvironmentVariable("SecretID"));
                        if (key.Item1)
                        {
                            sqlCheckerSettings.DataBases.Add(new DataBaseEntity() { ResourceId = item.GetValueOrDefault("ResourceId").ToString(), DataBaseConnection = key.Item2, Command = Environment.GetEnvironmentVariable("CheckCommand") });
                        }
                        else
                        {
                            throw new Exception($"Setting configuration error! Key Vault: " + key.Item2);
                        }
                    }
                    else
                    {
                        throw new Exception($"Setting configuration error! Not KVSecret1 found!");
                    }
                }
            }
            
           
            foreach (DataBaseEntity db in sqlCheckerSettings.DataBases)
            {
                
                try
                {
                    if (db != null && db.ResourceId != string.Empty)
                    {
                        log.LogInformation($"Access to DB {db.ResourceId}.");
                        if (db.DataBaseConnection == null || db.DataBaseConnection == string.Empty || db.Command == string.Empty)
                        {
                            result = new Tuple<bool, string, string>(false, "Setting configuration error", "Check database connection of " + db.ResourceId);
                        }
                        else
                        {
                            sqlCheck = new SQLDataAccess(db.DataBaseConnection, retryCount, initialInterval, increment, databaseTimeout);
                            result = sqlCheck.ExecuteReaderPolicy(db.Command, null);
                        }
                    }
                    else
                    {
                        result = new Tuple<bool, string, string>(false, "Setting configuration error", "Database not found or no ResourceId setted");
                    }
                }
                catch (Exception ex)
                {
                    result = new Tuple<bool, string, string>(false, ex.Message, ex.StackTrace);
                }
                db.IsAlive = result.Item1;
                db.Exception = new CustomException() { Message = result.Item2, StackTrace = result.Item3 };
                if (db.IsAlive)
                {
                    db.Retries = 0;
                }
                else
                {
                    db.Retries = db.Retries + 1;
                }
                log.LogInformation($"SQL Check Result {db.ResourceId} { result.Item1} {result.Item2}. {result.Item3}");
                await LogAnalyticsHelper.LogAnalyticsHelper.LogDataAsync(sqlCheckerSettings.LogAnalyticsWorkspaceId, logAnalyticsUrlFormat, sqlCheckerSettings.LogAnalyticsWorkspaceKey, logName, new { db.IsAlive, db.Retries, db.ResourceId, db.Exception });

            }
            if (sqlCheckerSettings.DataBases.Exists(item => !item.IsAlive))
            {
                int.TryParse(Environment.GetEnvironmentVariable("CriticalSchedule"), out nextCleanUpSeconds);
            }
            
            return new Tuple<int,SqlCheckerSettings>(nextCleanUpSeconds,sqlCheckerSettings);
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