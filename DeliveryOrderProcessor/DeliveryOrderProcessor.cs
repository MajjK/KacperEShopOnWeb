using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using KacperFunctionApp.Models;

namespace KacperFunctionApp
{
    public static class DeliveryOrderProcessor
    {
        private static readonly string _endpointUri = "https://kacper-cosmosdb-account.documents.azure.com:443/";
        private static readonly string _primaryKey = "oVqYZftO7TbyfMzQoXoFcyM0DNU4FvplchzSSxR5C1l2HQU06dA43viTHsxrRXYCIoF2R5fOPQ1XfqaJpHJTsA==";

        [FunctionName("DeliveryOrderProcessor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            if (req.Body != null)
            {
                CosmosClient client = new CosmosClient(_endpointUri, _primaryKey);
                DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync("eShopOnWeb");
                Database database = databaseResponse.Database;
                ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync("Orders", "/OrderId");
                Container container = containerResponse.Container;

                var bodyStr = "";
                using (StreamReader reader
                = new StreamReader(req.Body, Encoding.UTF8, true, 1024, true))
                {
                    bodyStr = reader.ReadToEnd();
                }

                log.LogInformation(bodyStr);
                Order order = JsonConvert.DeserializeObject<Order>(bodyStr);
                var response = await container.CreateItemAsync(order);

                log.LogInformation("Azure function processed a new order");
                return new OkObjectResult("New order was uploaded sucessfully");
            }
            else
            {
                return new BadRequestObjectResult("Please pass your order in the request body");
            }
        }
    }
}
