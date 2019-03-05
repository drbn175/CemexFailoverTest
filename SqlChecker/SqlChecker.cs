using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
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
            List<DataBaseEntity> dataBaseEntities = context.GetInput<List<DataBaseEntity>>();

            if (dataBaseEntities == null)
            {
                dataBaseEntities = JsonConvert.DeserializeObject<List<DataBaseEntity>>(Environment.GetEnvironmentVariable("DataBases"));
            }
            
            foreach(DataBaseEntity db in dataBaseEntities)
            {
                Tuple<bool, string,string> IsAlive = await context.CallActivityAsync<Tuple<bool, string, string>>("SqlChecker_Query", db);
                db.IsAlive = IsAlive.Item1;
                db.Exception = new CustomException() { Message=IsAlive.Item2, StackTrace=IsAlive.Item3 };

                int nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("Schedule"));
                if (!db.IsAlive)
                {
                    db.Retries++;
                    nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("CriticalSchedule"));
                }
                else
                {
                    db.Retries = 0;
                }
                DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(nextCleanUpSeconds);
                await context.CreateTimer(nextCleanup, CancellationToken.None);
                //Log Analytics 
                LogData(db);
                
            }
            context.ContinueAsNew(dataBaseEntities);
            
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
                        result = new Tuple<bool, string, string>(false, "local.setting.json configuration error", string.Empty);
                    }
                    else
                    {
                        SQLDataAccess sqlCheck = new SQLDataAccess(db.DataBaseConnection, retryCount, initialInterval, increment, databaseTimeout);
                        result = sqlCheck.ExecuteReaderPolicy(db.Command, null);
                    }
                }
                else
                {
                    result = new Tuple<bool, string, string>(false, "local.setting.json configuration error", string.Empty);
                }
            }catch(Exception ex)
            {
                result = new Tuple<bool, string, string>(false, ex.Message, ex.StackTrace);
            }
            log.LogInformation($"SQL CHeck Result { result.Item1} {result.Item2}. {result.Item3}");
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

        public static void LogData(object req)
        {
            string customerId = Environment.GetEnvironmentVariable("LogAnalyticsWorkspaceId");
            string sharedKey = Environment.GetEnvironmentVariable("LogAnalyticsWorkspaceKey");
            string LogName = Environment.GetEnvironmentVariable("AzureFunctionLog");
            var json = JsonConvert.SerializeObject(req);

            var datestring = DateTime.UtcNow.ToString("r");
            string stringToHash = "POST\n" + json.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, sharedKey);
            string signature = string.Format("SharedKey {0}:{1}", customerId, hashedString);

            PostData(signature, datestring, json, customerId, LogName);
        }


        public static string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Send a request to the POST API endpoint
        public static void PostData(string signature, string date, string json, string customerId, string LogName)
        {
            // You can use an optional field to specify the timestamp from the data. If the time field is not specified, Log Analytics assumes the time is the message ingestion time
            string TimeStampField = string.Empty;
            try
            {
                string url = string.Format(Environment.GetEnvironmentVariable("LogAnalyticsUrl"),customerId);

                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", LogName);
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", date);
                client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                Console.WriteLine(string.Format("Return Result: {0}", result));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("API Post Exception: {0}", ex.Message));
            }
        }
    }
}