using Azure;
using Azure.AI.Translation.Document;
using Azure.Communication.Email;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using NormalianTranslation.Web.Utilities;

namespace NormalianTranslation.Function
{
    public class Functions
    {
        // Blob Storage
        string sourceContainerName = "source";
        string targetContainerName = "translated";

        // https://www.red-gate.com/simple-talk/blogs/azure-functions-and-managed-identity-more-secrets/
        [FunctionName("TranslateFunction")]
        public async Task Run([BlobTrigger("source/{name}", Connection = "STORAGE-CONNECTIONSTRING")] Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            log.LogInformation($"C# Blob trigger DoTranslate function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
           
            BlobServiceClient blobServiceClient = new BlobServiceClient(await KeyVaultService.GetSecret("STORAGE-CONNECTIONSTRING"));
            BlobContainerClient sourceClient = blobServiceClient.GetBlobContainerClient(sourceContainerName);
            BlobContainerClient targetClient = blobServiceClient.GetBlobContainerClient(targetContainerName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(name);
            BlobClient targetBlobClient = targetClient.GetBlobClient(name);

            Uri sourceUri;
            Uri targetUri;
            if (sourceClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one hour.
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = sourceContainerName,
                    Resource = "c",
                };
                sasBuilder.SetPermissions(BlobContainerSasPermissions.All);
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(24);
                sourceUri = sourceClient.GenerateSasUri(sasBuilder);
            }
            else
            {
                log.LogError($"{sourceClient.Name} can't be created SAS token");
                throw new Exception($"{sourceClient.Name} can't be created SAS token");
            }
            if (targetClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for one hour.
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = targetContainerName,
                    Resource = "c",
                };
                sasBuilder.SetPermissions(BlobContainerSasPermissions.All);
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(24);
                targetUri = targetClient.GenerateSasUri(sasBuilder);
            }
            else
            {
                log.LogError($"{targetClient.Name} can't be created SAS token");
                throw new Exception($"{targetClient.Name} can't be created SAS token");
            }

            string targetLanguage = sourceClient.GetBlobClient(name).GetProperties().Value.Metadata["target_language"];
            DocumentTranslationClient client = 
                new DocumentTranslationClient(new Uri(await KeyVaultService.GetSecret("AITRANSLATOR-ENDPOINT")), 
                new AzureKeyCredential(await KeyVaultService.GetSecret("AITRANSLATOR-SUBSCRIPTIONID")));
            DocumentTranslationInput input = new DocumentTranslationInput(sourceUri, targetUri, targetLanguage);
            log.LogInformation($"  Translate documento to {targetLanguage}, Source:{sourceUri}, Target:{targetUri}");

            input.Source.Prefix = name;
            DocumentTranslationOperation operation = await client.StartTranslationAsync(input);

            await operation.WaitForCompletionAsync();

            log.LogInformation($"  Status: {operation.Status}");
            log.LogInformation($"  Created on: {operation.CreatedOn}");
            log.LogInformation($"  Last modified: {operation.LastModified}");
            log.LogInformation($"  Total documents: {operation.DocumentsTotal}");
            log.LogInformation($"    Succeeded: {operation.DocumentsSucceeded}");
            log.LogInformation($"    Failed: {operation.DocumentsFailed}");
            log.LogInformation($"    In Progress: {operation.DocumentsInProgress}");
            log.LogInformation($"    Not started: {operation.DocumentsNotStarted}");

            await foreach (DocumentStatusResult document in operation.Value)
            {
                log.LogInformation($"Document with Id: {document.Id}");
                log.LogInformation($"  Status:{document.Status}");
                if (document.Status == DocumentTranslationStatus.Succeeded)
                {
                    log.LogInformation($"  Translated Document Uri: {document.TranslatedDocumentUri}");
                    log.LogInformation($"  Translated to language: {document.TranslatedToLanguageCode}.");
                    log.LogInformation($"  Document source Uri: {document.SourceDocumentUri}");
                    // Status will be changed from "Succeeded" to "Failed", so this is required to stop sending mis-error mail
                    break;
                }
                else
                {
                    log.LogError($"  Error Code: {document.Error.Code}");
                    log.LogError($"  Status: {document.Status}");
                    log.LogError($"  Message: {document.Error.Message}");
                    //await SendMail(request.EmailAddress, $"Error Translation - {name}", $"{name} is failed to translate, because ${document.Error.Message}. Status is {document.Status}", "");
                }
            }
        }


        [FunctionName("SendNotification")]
        public async Task SendNotification([BlobTrigger("translated/{name}", Connection = "STORAGE-CONNECTIONSTRING")] Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger SendNotification function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            // Blob
            BlobServiceClient blobServiceClient = new BlobServiceClient(await KeyVaultService.GetSecret("STORAGE-CONNECTIONSTRING"));
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(targetContainerName);
            BlobClient blobClient = containerClient.GetBlobClient(name);

            if (blobClient.CanGenerateSasUri)
            {
                // Create a SAS token that's valid for 24 hours.
                BlobSasBuilder sasBuilder = new BlobSasBuilder()
                {
                    BlobContainerName = targetContainerName,
                    Resource = "b"
                };
                sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddHours(24);
                sasBuilder.SetPermissions(BlobContainerSasPermissions.All);
                var contentUri = blobClient.GenerateSasUri(sasBuilder);

                var username = name.Substring(0, name.IndexOf('/'));
                var toAddress = $"{username}@microsoft.com";
                log.LogInformation($"################# Send {name} URL to {toAddress}.");
                await SendMail(toAddress,
                    $"Success Translation - {name}",
                    $"Now, {name} is completed to translate",
                    $"your content <a href='{contentUri}'>{name}</a> has been translated",
                    log);
            }
            else
            {
                log.LogError($"{name} can't be created SAS");
                throw new Exception($"{name} can't be created SAS");
            }
        }

        public async Task SendMail(string toAddress, string subject, string message, string htmlContent, ILogger log)
        {
            EmailClient emailClient = new EmailClient(await KeyVaultService.GetSecret("ACS-CONNECTIONSTRING"));

            try
            {
                log.LogInformation("Sending email...");
                EmailSendOperation emailSendOperation = await emailClient.SendAsync(
                    Azure.WaitUntil.Completed,
                    await KeyVaultService.GetSecret("SENDER-ADDRESS"),
                    toAddress,
                    subject,
                    htmlContent);
                EmailSendResult statusMonitor = emailSendOperation.Value;

                log.LogInformation($"Email Sent. Status = {emailSendOperation.Value.Status}");

                /// Get the OperationId so that it can be used for tracking the message for troubleshooting
                string operationId = emailSendOperation.Id;
                log.LogInformation($"Email operation id = {operationId}");
            }
            catch (RequestFailedException ex)
            {
                /// OperationID is contained in the exception message and can be used for troubleshooting purposes
                log.LogError($"Email send operation failed with error code: {ex.ErrorCode}, message: {ex.Message}");
            }
        }
    }
}
