using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NormalianTranslation.Web.Utilities
{
    public static class KeyVaultService
    {
        // Key Vault
        const string KEYVAULT_URI = "https://keyvaulttranslatewestus3.vault.azure.net/";
        static readonly SecretClient keyvaultClient = null;

        static KeyVaultService()
        {
            keyvaultClient = new SecretClient(new Uri(KEYVAULT_URI),
                new DefaultAzureCredential(new DefaultAzureCredentialOptions()
                {
#if DEBUG == true
                    // Use Azure Cli Credential for local development
                    ExcludeEnvironmentCredential = true,
                    ExcludeWorkloadIdentityCredential = true,
                    ExcludeManagedIdentityCredential = true,
                    ExcludeSharedTokenCacheCredential = true,
                    ExcludeVisualStudioCredential = true,
                    ExcludeVisualStudioCodeCredential = true,
                    TenantId = "b7501d50-50bf-4080-bfaa-912394380b1a",
#endif
                })
            );
        }

        public async static Task<string> GetSecret(string secretName)
        {
            Response<KeyVaultSecret> secret = await keyvaultClient.GetSecretAsync(secretName);
            return secret.Value.Value;
        }
    }
}
