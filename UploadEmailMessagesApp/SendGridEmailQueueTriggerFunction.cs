using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;

namespace UploadEmailMessagesApp
{
    public class SendGridEmailQueueTriggerFunction
    {
        [FunctionName("SendGridEmailQueueTriggerFunction")]
        public void Run([QueueTrigger("email-queue", Connection = "CloudStorageAccount")]string myQueueItem,
            [SendGrid(ApiKey = "SendgridAPIKey")] out SendGridMessage sendGridMessage,
            ILogger log,
            ExecutionContext context)
        {
            log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");

            try
            {
                var queueItem = myQueueItem.ToString();

                dynamic jsonData = JObject.Parse(queueItem);
                string emailBody = jsonData.Content;
                string to = jsonData.To;
                string cc = jsonData.Cc;
                string templateId = jsonData.TemplateID;
                var data = jsonData.Data;
                var attachments = jsonData.Attachments;

                sendGridMessage = new SendGridMessage
                {
                    From = new EmailAddress("", "AzureFuncApps"),
                };
                sendGridMessage.AddTo(to);
                sendGridMessage.AddCc(cc);
                sendGridMessage.SetTemplateId(templateId);
                sendGridMessage.SetTemplateData(data);

                foreach (string attachment in attachments)
                {
                    string[] parts = attachment.Split('/');
                    string container = parts[0];
                    string fileName = parts[1];

                    sendGridMessage.AddAttachment(fileName, GetFileFromBlob(container, fileName, context).Result);
                }

                //sendGridMessage.SetSubject("Awesome Azure Function app");
                //sendGridMessage.AddContent("text/html", emailBody);
            }
            catch (Exception ex)
            {
                sendGridMessage = new SendGridMessage();
                log.LogError($"Error occured while processing QueueItem {myQueueItem} , Exception - {ex.InnerException}");

            }
        }

        private async Task<string> GetFileFromBlob(string container, string fileName, ExecutionContext context)
        {

            CloudStorageAccount storageAccount = GetCloudStorageAccount(context);

            // Connect to the blob storage
            CloudBlobClient serviceClient = storageAccount.CreateCloudBlobClient();
            // Connect to the blob container
            CloudBlobContainer blobContainer = serviceClient.GetContainerReference($"{container}");
            // Connect to the blob file
            CloudBlockBlob blob = blobContainer.GetBlockBlobReference($"{fileName}");
            // Get the blob file as text
            //string contents = blob.DownloadTextAsync().Result;

            string b64Content;

            await blob.FetchAttributesAsync();//Fetch blob's properties
            byte[] arr = new byte[blob.Properties.Length];
            await blob.DownloadToByteArrayAsync(arr, 0);
            b64Content = Convert.ToBase64String(arr);

            return b64Content;
        }

        private static CloudStorageAccount GetCloudStorageAccount(ExecutionContext executionContext)
        {
            var config = new ConfigurationBuilder().SetBasePath(executionContext.FunctionAppDirectory)
                                                   .AddJsonFile("local.settings.json", true, true)
                                                   .AddEnvironmentVariables()
                                                   .Build();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(config["CloudStorageAccount"]);
            return storageAccount;
        }
    }
}
