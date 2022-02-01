using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace KacperFunctionApp
{
    public static class OrderItemsReserver
    {
        [FunctionName("OrderItemsReserver")]
        [FixedDelayRetry(2, "00:00:10")]
        public static async Task Run([ServiceBusTrigger("kacperqueue", Connection = "KacperServiceBusConnectionString")] string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");

            if (myQueueItem != null)
            {
                try
                {
                    BlobServiceClient blobServiceClient = new BlobServiceClient("DefaultEndpointsProtocol=https;AccountName=kacperstorageaccount;AccountKey=Gkama+tMAw2LUJtF5BQx2wF5tOM/SALkSzeeoAXgYii9x5/3mLvLMICxcpPUIsPoGH9GuyJjAEPdaxUapr2KzA==;EndpointSuffix=core.windows.net");
                    //BlobServiceClient blobServiceClient = new BlobServiceClient("ybylylblyyl");
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("kacpercontainer");
                    var orderedItems = await CountOrderedItems(containerClient);
                    BlobClient blobClient = containerClient.GetBlobClient($"Order_{orderedItems}");

                    byte[] byteArray = Encoding.ASCII.GetBytes(myQueueItem);
                    MemoryStream stream = new MemoryStream(byteArray);

                    var response = blobClient.UploadAsync(stream);
                    log.LogInformation($"Successfully uploaded new order: {myQueueItem}");
                }
                catch (Exception ex)
                {
                    HttpClient _client = new HttpClient();
                    var url = "https://prod-54.northeurope.logic.azure.com:443/workflows/f68984cce97745b89c32d4d9e7c85a31/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=8bStJRPh7xIpKihgas8YfJfw6JqWgsKTQl2tsNVrYrc";
                    HttpResponseMessage response = await _client.PostAsync(url, new StringContent(myQueueItem, Encoding.UTF8, "application/json"));
                    log.LogInformation($"Problems with uploading new order. The order was sent to your email address");
                }
            }
        }

        static async Task<int> CountOrderedItems(BlobContainerClient containerClient)
        {
            AsyncPageable<BlobItem> blobs = containerClient.GetBlobsAsync();
            List<string> blobNames = new List<string>();
            await foreach (var blob in blobs)
            {
                blobNames.Add(blob.Name);
            }

            return blobNames.Count;
        }
    }
}
