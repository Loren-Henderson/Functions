using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;


namespace Mk.Function
{
    public static class DurableFunctionsOrchestrationTicTacToe
    {
        static string _testMessage; 
        [FunctionName("DurableFunctionsOrchestrationTicTacToe")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            
            
            var timeoutCts = new CancellationTokenSource();

            var requestTimeoutAt = context.CurrentUtcDateTime.AddSeconds(60);
            
            var timeoutTask = context.CreateTimer(requestTimeoutAt, timeoutCts.Token);
            
            var serverReadyTask = context.WaitForExternalEvent<string>("EventCheck");
            
            var nextEvent = await Task.WhenAny(timeoutTask, serverReadyTask);

            var outputs = new List<string>();
            string serverInfo;

             if (nextEvent == serverReadyTask)
            {
                // Get server IP:Port from the event body
                serverInfo = serverReadyTask.Result;
                log.LogInformation($"serverInfo inside if= '{serverInfo}'" );
                
            }
            else // Timeout happened
            {
                serverInfo = "timed out";
                 log.LogInformation($"timeoutInfo serverInfo= '{serverInfo}'" );
            }

            if (!timeoutTask.IsCompleted)
            {
                log.LogInformation($"About to cancel" );
                timeoutCts.Cancel();
            }

            
            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("DurableFunctionsOrchestrationTicTacToe_Hello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("DurableFunctionsOrchestrationTicTacToe_Hello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("DurableFunctionsOrchestrationTicTacToe_Hello", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("DurableFunctionsOrchestrationTicTacToe_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("DurableFunctionsOrchestrationTicTacToe_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            OrchestrationStatusQueryCondition status = new OrchestrationStatusQueryCondition();
           
            List<Microsoft.Azure.WebJobs.Extensions.DurableTask.OrchestrationRuntimeStatus> sL = new List<Microsoft.Azure.WebJobs.Extensions.DurableTask.OrchestrationRuntimeStatus>();
            sL.Add(OrchestrationRuntimeStatus.Running);
            sL.Add(OrchestrationRuntimeStatus.Completed);
            status.RuntimeStatus = sL;
            
            
            string instanceId;
            // Function input comes from the request content.
            if(string.IsNullOrEmpty(_testMessage)){
                instanceId = await starter.StartNewAsync("DurableFunctionsOrchestrationTicTacToe", null);
                _testMessage = instanceId;
            }else{
                instanceId = await starter.StartNewAsync<string>("DurableFunctionsOrchestrationTicTacToe", _testMessage, null);
            }
            

            var openInstances = await starter.GetStatusAsync(instanceId, true, false, false);
            log.LogInformation($"This is number of running instances:  '{openInstances.RuntimeStatus}'");
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            log.LogInformation($"Test Mesage = '{_testMessage}'.");
            
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("CallSayHello")]
        public static async Task<HttpResponseMessage> CheckSayHello (
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
            {
                // var currentStatus = await starter.GetStatusAsync(instanceId, true, false, false);
                // log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
                
               
               // string oId = req.Query["oId"];
                string name = req.Query["name"];
                if(!string.IsNullOrEmpty(_testMessage)){
                    // Leveraging the fact that the orchestrator instance ID is the player GUID
                    await starter.RaiseEventAsync(_testMessage, "EventCheck", name); 
                }
                
                return new HttpResponseMessage();
                
            }
    }
}