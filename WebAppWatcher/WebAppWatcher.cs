using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
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
                WebAppWatcherSettings webAppWatcherSettings = context.GetInput<WebAppWatcherSettings>();

                if (webAppWatcherSettings == null)
                {
                    webAppWatcherSettings = new WebAppWatcherSettings();
                }
                if (webAppWatcherSettings.WebApis == null)
                {
                    webAppWatcherSettings.WebApis = JsonConvert.DeserializeObject<List<WebApiEntity>>(Environment.GetEnvironmentVariable("WebApis"));
                }
                //Log Analytics 
                webAppWatcherSettings.LogName = Environment.GetEnvironmentVariable("AzureFunctionLog");
                if (webAppWatcherSettings.LogName == null || webAppWatcherSettings.LogName == string.Empty)
                {
                    throw new Exception($"Setting configuration error! AzureFunctionLog");
                }
                webAppWatcherSettings.LogAnalyticsUrlFormat = Environment.GetEnvironmentVariable("LogAnalyticsUrl");
                if (webAppWatcherSettings.LogAnalyticsUrlFormat == null || webAppWatcherSettings.LogAnalyticsUrlFormat == string.Empty)
                {
                    throw new Exception($"Setting configuration error! LogAnalyticsUrl");
                }
                //await webAppWatcherSettings.LoginAsync();
                await context.CallActivityAsync<string>("WebAppWatcher_Check", webAppWatcherSettings);
                
                int nextCleanUpSeconds = System.Convert.ToInt32(Environment.GetEnvironmentVariable("Schedule"));
                DateTime nextCleanup = context.CurrentUtcDateTime.AddSeconds(nextCleanUpSeconds);
                await context.CreateTimer(nextCleanup, CancellationToken.None);
                context.ContinueAsNew(webAppWatcherSettings);
            }
            catch (Exception ex)
            {
                context.SetCustomStatus($"Setting configuration error! { ex.Message } { ex.StackTrace }");
            }
        }

        [FunctionName("WebAppWatcher_Check")]
        public static async Task WebAppWatcher_Check([ActivityTrigger] WebAppWatcherSettings webAppWatcherSettings,  ILogger log)
        {
            string result = null;
            string statusCode = null;
            DateTime time;
            TimeSpan took;
            foreach (WebApiEntity webapp in webAppWatcherSettings.WebApis)
            {
                //Logic to identify webapp failing
                try
                {
                    time = DateTime.Now;
                    log.LogInformation($"Saying hello to {webapp.Url}.");
                    //if (webapp.IsSoap)
                    //{
                    //    SoapRequestHelper.SoapRequestHelper.TestMethod();
                    //}
                    //else
                    //{
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(webapp.Url);
                        request.CookieContainer = new CookieContainer();
                        foreach (WebApiCookie c in webAppWatcherSettings.CustomCookies)
                        {
                            request.CookieContainer.Add(new Cookie(c.name, c.value, string.Empty, new Uri(webapp.Url).Host));
                        }

                        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                        using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                        using (Stream stream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            result = await reader.ReadToEndAsync();
                            statusCode = response.StatusCode.ToString();
                        }

                        took = DateTime.Now.Subtract(time);
                    //}
                   

                    await LogAnalyticsHelper.LogAnalyticsHelper.LogDataAsync(webAppWatcherSettings.LogAnalyticsWorkspaceId, webAppWatcherSettings.LogAnalyticsUrlFormat, webAppWatcherSettings.LogAnalyticsWorkspaceKey, webAppWatcherSettings.LogName, new { webapp.Url, Duration= took.Milliseconds, OuterMessage = "Success", StatusCode= statusCode });
                }
                catch (Exception ex)
                {
                    await LogAnalyticsHelper.LogAnalyticsHelper.LogDataAsync(webAppWatcherSettings.LogAnalyticsWorkspaceId, webAppWatcherSettings.LogAnalyticsUrlFormat, webAppWatcherSettings.LogAnalyticsWorkspaceKey, webAppWatcherSettings.LogName, new { webapp.Url, Duration = -1, OuterMessage = ex.Message, StatusCode = statusCode });
                }
            }
           
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