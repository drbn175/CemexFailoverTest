using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LogAnalyticsHelper
{
    public static class LogAnalyticsHelper
    {
        public static async Task LogDataAsync(string customerId, string logAnalyticsUrl, string sharedKey, string logName , object req)
        {
            var json = JsonConvert.SerializeObject(req);

            var datestring = DateTime.UtcNow.ToString("r");
            string stringToHash = "POST\n" + json.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, sharedKey);
            string signature = string.Format("SharedKey {0}:{1}", customerId, hashedString);

            await PostDataAsync(signature, datestring, json, logAnalyticsUrl, customerId, logName);
        }


        private static string BuildSignature(string message, string secret)
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
        private static async Task PostDataAsync(string signature, string date, string json, string logAnalyticsUrl, string customerId, string logName)
        {
            // You can use an optional field to specify the timestamp from the data. If the time field is not specified, Log Analytics assumes the time is the message ingestion time
            string TimeStampField = string.Empty;
            try
            {
                string url = string.Format(logAnalyticsUrl, customerId);

                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", logName);
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", date);
                client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

                System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
                await responseContent.ReadAsStringAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("API Post Exception: {0}", ex.Message));
            }
        }
    }
}
