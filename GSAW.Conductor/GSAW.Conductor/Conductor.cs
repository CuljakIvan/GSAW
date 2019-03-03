using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using GSAW.Conductor.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GSAW.Conductor
{
    public static class Conductor
    {
        private const string ServiceBusConnectionString = "YOUR_SERVICE_BUS_CONNECTION_STRING_HERE";
        private const string ServiceBusConductorQueueName = "requestsForConductor";
        private const string ServiceBusCarrierQueueName = "requestsForCarrier";

        [FunctionName("ConductorQueueListener")]
        public static async Task Run([ServiceBusTrigger(ServiceBusConductorQueueName, Connection = nameof(ServiceBusConnectionString))]Message message,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            var workRequest = JsonConvert.DeserializeObject<WorkRequest>(Encoding.UTF8.GetString(message.Body));

            string instanceId = await starter.StartNewAsync("Conductor", workRequest);
        }

        [FunctionName("Conductor_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("Conductor", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("Conductor")]
        public static async Task<object> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var workRequest = context.GetInput<WorkRequest>();
            if (workRequest == null)
            {
                workRequest =
                    await context.CallActivityAsync<WorkRequest>(nameof(FetchWorkRequestFromQueue), context);
            }

            var httpManagementPayload =
                await context.CallActivityAsync<HttpManagementPayload>(nameof(GetAndReturnHttpManagementPayload), context);
            var callbackUrl = httpManagementPayload.SendEventPostUri.Replace("{eventName}", "Callback",
                StringComparison.InvariantCultureIgnoreCase);
            workRequest.CallbackUrl = callbackUrl;

            var sendToCarrierSuccess =
                await context.CallActivityAsync<bool>(nameof(SendWorkRequestToCarrier),
                    workRequest);

            object result = await context.WaitForExternalEvent<object>("Callback");

            workRequest.Result = result;
            var sendToWebhookSuccess =
                await context.CallActivityAsync<bool>(nameof(SendResultToWebhook), workRequest);

            return new {WorkRequestId = workRequest.WorkRequestId, Result = workRequest.Result};
        }

        [FunctionName(nameof(GetAndReturnHttpManagementPayload))]
        public static HttpManagementPayload GetAndReturnHttpManagementPayload(
            [ActivityTrigger] DurableActivityContext ctx,
            [OrchestrationClient] DurableOrchestrationClient client)
        {
            var httpManagementPayload = client.CreateHttpManagementPayload(ctx.InstanceId);
            return httpManagementPayload;
        }

        [FunctionName(nameof(FetchWorkRequestFromQueue))]
        public static async Task<WorkRequest> FetchWorkRequestFromQueue([ActivityTrigger] DurableActivityContext ctx)
        {
            var messageReceiver = new MessageReceiver(ServiceBusConnectionString, ServiceBusConductorQueueName);
            var messages = await messageReceiver.ReceiveAsync(1, TimeSpan.FromSeconds(10));
            var message = messages?.FirstOrDefault();

            if (message != null)
            {
                var workRequest = JsonConvert.DeserializeObject<WorkRequest>(Encoding.UTF8.GetString(message.Body));

                await messageReceiver.CompleteAsync(message.SystemProperties.LockToken);

                return workRequest;
            }
            else
            {
                return null;
            }
        }

        [FunctionName(nameof(SendWorkRequestToCarrier))]
        public static async Task<bool> SendWorkRequestToCarrier([ActivityTrigger] WorkRequest workRequest)
        {
            try
            {
                var queueClient = new QueueClient(ServiceBusConnectionString, ServiceBusCarrierQueueName);
                await queueClient.SendAsync(
                    new Message(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(workRequest))));
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        [FunctionName(nameof(SendResultToWebhook))]
        public static async Task<bool> SendResultToWebhook([ActivityTrigger] WorkRequest workRequest)
        {
            try
            {
                var httpClient = new HttpClient();
                var response = await httpClient.PostAsync(workRequest.WebhookUrl,
                    new StringContent(JsonConvert.SerializeObject(workRequest.Result)));

                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}