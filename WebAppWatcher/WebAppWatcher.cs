using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace WebAppWatcher
{
    public static class WebAppWatcher
    {
        [FunctionName("WebAppWatcher")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            try
            {
                WebAppWatcherSettings WebAppWatcherSettings = context.GetInput<WebAppWatcherSettings>();

                if (WebAppWatcherSettings == null)
                {
                    WebAppWatcherSettings = new WebAppWatcherSettings();
                }
                if (WebAppWatcherSettings.WebApis == null)
                {
                    WebAppWatcherSettings.WebApis = JsonConvert.DeserializeObject<List<WebApiEntity>>(Environment.GetEnvironmentVariable("DataBases"));
                }

                foreach (WebApiEntity webapp in WebAppWatcherSettings.WebApis)
                {
                    //Logic to identify webapp failing
                }

                int nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("Schedule"));
                DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(nextCleanUpSeconds);
                await context.CreateTimer(nextCleanup, CancellationToken.None);

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
                foreach (WebApiEntity webapp in WebAppWatcherSettings.WebApis)
                {

                    await LogAnalyticsHelper.LogAnalyticsHelper.LogDataAsync(WebAppWatcherSettings.LogAnalyticsWorkspaceId, logAnalyticsUrlFormat, WebAppWatcherSettings.LogAnalyticsWorkspaceKey, logName, webapp);

                }
                context.ContinueAsNew(WebAppWatcherSettings);
            }
            catch (Exception ex)
            {
                context.SetCustomStatus($"Setting configuration error! { ex.Message } { ex.StackTrace }");
            }
        }

        [FunctionName("WebAppWatcher_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("WebAppWatcher_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("WebAppWatcher", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}