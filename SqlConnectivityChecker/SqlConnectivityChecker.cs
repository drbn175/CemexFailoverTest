using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace SqlConnectivityChecker
{
    public static class SqlConnectivityChecker
    {
        [FunctionName("SqlConnectivityChecker")]
        public static void Run([TimerTrigger("%schedule%")]TimerInfo timer, ILogger log)
        {
            log.LogInformation($"SqlConnectivityChecker executed at: {DateTime.Now}");
            if (timer.IsPastDue)
            {
                log.LogInformation($"SqlConnectivityChecker delayed executed");
            }
            bool isAlive = IsAlive();
            if (!isAlive)
            {
                //Ejecutar tarea cada 30 segundos (configurable)
                
            }

        }

        private static bool IsAlive() {
            return true;
        }

        private static Task<bool> IsDead()
        {

        }
       
    }
}
