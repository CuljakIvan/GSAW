using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;

namespace GSAW.Carrier
{
    class Program
    {
        private const string ServiceBusConnectionString = "YOUR_SERVICE_BUS_CONNECTION_STRING_HERE";
        private const string ServiceBusCarrierQueueName = "requestsForCarrier";
        private const string CognitiveServicesSubscriptionKey = "YOUR_COGNITIVE_SERVICES_SUBSCRIPTION_KEY_HERE";
        private const string CognitiveServicesUriBase = "https://westeurope.api.cognitive.microsoft.com/vision/v2.0/analyze";

        private static readonly HttpClient _httpClient = new HttpClient();

        static void Main(string[] args)
        {
            var queueClient = new QueueClient(ServiceBusConnectionString, ServiceBusCarrierQueueName);
            queueClient.RegisterMessageHandler(Handler, ExceptionReceivedHandler);

            Console.ReadLine();
        }

        private static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs arg)
        {
            return null;
        }

        private static async Task Handler(Message message, CancellationToken cancellationToken)
        {
            var body = message.Body;
            var workRequestDefinition = new { CallbackUrl = "", ImageUrl = "", WorkRequestId = "" };
            var workRequest = JsonConvert.DeserializeAnonymousType(Encoding.UTF8.GetString(body), workRequestDefinition);

            var requestParameters = "visualFeatures=Categories,Description,Color";
            var analyzeRequest =
                new HttpRequestMessage(HttpMethod.Post, $"{CognitiveServicesUriBase}?{requestParameters}")
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new { url = workRequest.ImageUrl }), Encoding.UTF8, "application/json")
                };
            analyzeRequest.Headers.Add("Ocp-Apim-Subscription-Key", CognitiveServicesSubscriptionKey);

            var analyzeResponse = await _httpClient.SendAsync(analyzeRequest, cancellationToken);


            var callbackRequest = new HttpRequestMessage(HttpMethod.Post, workRequest.CallbackUrl)
            {
                Content = analyzeResponse.Content
            };

            var callbackResponse = await _httpClient.SendAsync(callbackRequest, cancellationToken);
        }
    }
}
