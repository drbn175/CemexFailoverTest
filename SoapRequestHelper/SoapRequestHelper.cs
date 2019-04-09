using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoapRequestHelper
{
    public static class SoapRequestHelper
    {
        public static ServiceTest.apiQueryResult TestMethod() {
            using (ServiceTest.ApiServiceClient api = new ServiceTest.ApiServiceClient()){
                api.Open();
                var result = api.getCertifications("null", 1);
                return result;
            }
        }
    }

}
