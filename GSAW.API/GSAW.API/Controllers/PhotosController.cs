using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GSAW.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace GSAW.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private const string StorageConnectionString = "YOUR_STORAGE_CONNECTION_STRING_HERE";
        private const string StorageContainerName = "photos";
        private const string ServiceBusConnectionString = "YOUR_SERVICE_BUS_CONNECTION_STRING_HERE";
        private const string ServiceBusConductorQueueName = "requestsForConductor";
        private const string TopicEndpoint = "YOUR_EVENTGRID_TOPIC_ENDPOINT_URL_HERE";
        private const string TopicKey = "YOUR_EVENTGRID_TOPIC_KEY_HERE";

        private readonly List<string> _allowedExtensions = new List<string>() { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        private readonly CloudBlobClient _blobClient;
        private readonly QueueClient _queueClient;

        public PhotosController()
        {
            var storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            _blobClient = storageAccount.CreateCloudBlobClient();

            _queueClient = new QueueClient(ServiceBusConnectionString, ServiceBusConductorQueueName);
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            return StatusCode(200, new {message = "Well hello... this seems to be working ;)"});
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] GenericMultipartFormModel model)
        {
            var workRequestId = Guid.NewGuid().ToString();

            try
            {
                if (model?.Source == null)
                {
                    return StatusCode(400, new { Error = "Source missing", WorkRequestId = workRequestId });
                }

                if (string.IsNullOrWhiteSpace(model.Webhook))
                {
                    return StatusCode(400, new { Error = "Webhook missing", WorkRequestId = workRequestId });
                }

                var sourceFilenameExtension =
                    Path.GetExtension(model.Source.FileName.Replace("\"", string.Empty));

                if (!Enumerable.Any<string>(_allowedExtensions,
                    x => x.Equals(sourceFilenameExtension, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return StatusCode(415, new { Error = "Unallowed extension", WorkRequestId = workRequestId });
                }

                var container = _blobClient.GetContainerReference(StorageContainerName);
                await container.CreateIfNotExistsAsync();

                var sourceBlockBlob = container.GetBlockBlobReference($"{workRequestId}{sourceFilenameExtension}");
                await sourceBlockBlob.UploadFromStreamAsync(model.Source.OpenReadStream());

                var imageUrl = sourceBlockBlob.Uri.AbsoluteUri +
                               sourceBlockBlob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
                               {
                                   Permissions = SharedAccessBlobPermissions.Read,
                                   SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddYears(99),
                                   SharedAccessStartTime = DateTimeOffset.UtcNow.AddYears(-1)
                               });

                var workRequest = new WorkRequest() { WorkRequestId = workRequestId, ImageUrl = imageUrl, WebhookUrl = model.Webhook };

                await _queueClient.SendAsync(
                    new Message(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(workRequest))));
                
                string topicHostname = new Uri(TopicEndpoint).Host;
                TopicCredentials topicCredentials = new TopicCredentials(TopicKey);
                EventGridClient client = new EventGridClient(topicCredentials);

                await client.PublishEventsAsync(topicHostname, new List<EventGridEvent>()
                {
                    new EventGridEvent()
                    {
                        Id = workRequestId,
                        EventType = "GSAW.Photos.WorkRequestReceived",
                        Data = workRequest,
                        EventTime = DateTime.Now,
                        Subject = "BLUE",
                        DataVersion = "2.0"
                    }
                });
                
                return StatusCode(202, new { WorkRequestId = workRequestId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = "Something went wrong", WorkRequestId = workRequestId });
            }
        }
    }
}
