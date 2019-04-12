using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyVaultHelper
{
    public static class KeyVaultHelper
    {
        

        public static async Task<Tuple<bool, string>> GetValueAsync(string keyVaultUrlBase, string secretName, string clientId, string secretId)
        {
            string value = string.Empty;
            Tuple<bool, string> result;
            try
            {
                AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();
                KeyVaultClient keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new KeyVaultClient.AuthenticationCallback(async (string authority, string resource, string scope) =>
                {
                    resource = "https://vault.azure.net";
                    var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
                    ClientCredential clientCred = new ClientCredential(clientId, secretId);
                    var authResult = await context.AcquireTokenAsync(resource, clientCred);
                    return authResult.AccessToken;
                    //return await azureServiceTokenProvider.GetAccessTokenAsync("https://vault.azure.net");
                })));
                var secret = await keyVaultClient.GetSecretAsync(keyVaultUrlBase,secretName)
                        .ConfigureAwait(false);
                value = secret.Value;
                result = new Tuple<bool, string>(true, value);
            }
            /* If you have throttling errors see this tutorial https://docs.microsoft.com/azure/key-vault/tutorial-net-create-vault-azure-web-app */
            /// <exception cref="KeyVaultErrorException">
            /// Thrown when the operation returned an invalid status code
            /// </exception>
            catch (KeyVaultErrorException keyVaultException)
            {
                result = new Tuple<bool, string>(false, keyVaultException.Message);
            }
            return result;
        }

       

    }
}
