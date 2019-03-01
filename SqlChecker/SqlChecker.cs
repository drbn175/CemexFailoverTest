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

namespace SqlChecker
{
    public static class SqlChecker
    {
        [FunctionName("SqlChecker")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            int retries = context.GetInput<int>();
            bool IsAlive = await context.CallActivityAsync<bool>("SqlChecker_Query", "dbConnection");

            int nextCleanUpSeconds = IsAlive ? System.Convert.ToInt32(Environment.GetEnvironmentVariable("Schedule")) : System.Convert.ToInt32(Environment.GetEnvironmentVariable("CriticalSchedule"));

            DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(nextCleanUpSeconds);
            await context.CreateTimer(nextCleanup, CancellationToken.None);

            if (!IsAlive)
            {
                retries++;
                if(retries >= System.Convert.ToInt32(Environment.GetEnvironmentVariable("MaxRetriesToSendAlert")))
                {
                    //Log Analytics
                    LogData(new object());
                }
            }
            
            context.ContinueAsNew(IsAlive ? 0 : retries);
        }

        [FunctionName("SqlChecker_Query")]
        public static bool DoQuery([ActivityTrigger] string dbConnection, ILogger log)
        {
            log.LogInformation($"Access to DB {dbConnection}.");
            var result = false;
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
            string customerId = "LogAnalyticsWorkspaceId";
            string sharedKey = "LogAnalyticsWorkspaceKey";
            string LogName = "AzureFunctionLog";
            var json = JsonConvert.SerializeObject(req);

            var datestring = DateTime.UtcNow.ToString("r");
            string stringToHash = "POST\n" + json.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, sharedKey);
            string signature = "SharedKey " + customerId + ":" + hashedString;

            PostData(signature, datestring, json, customerId, LogName);
        }

        // Build the API signature
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
            string TimeStampField = "";
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
                Console.WriteLine("Return Result: " + result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }
    }
}