using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Newtonsoft.Json.Linq;
using IoTManager;


var iotHubConn = ResourcesManager.iotHubConnectionString;
var serviceBusConn = ResourcesManager.serviceBusConnectionString;

using var devRegistry = RegistryManager.CreateFromConnectionString(iotHubConn);

var iotHubManager = new IoTHubManager( devRegistry);

await using var busClient = new ServiceBusClient(serviceBusConn);
await using var kpiQueue = busClient.CreateProcessor(ResourcesManager.KPIQueueName);
await using var errQueue = busClient.CreateProcessor(ResourcesManager.deviceErrorQueue);


kpiQueue.ProcessMessageAsync += KpiMessageHandler;
kpiQueue.ProcessErrorAsync += CommonErrorHandler;

errQueue.ProcessMessageAsync += ErrorMsgHandler;
errQueue.ProcessErrorAsync += CommonErrorHandler;


await kpiQueue.StartProcessingAsync();
await errQueue.StartProcessingAsync();

System.Console.WriteLine(">>> System gotowy. ENTER zatrzymuje.");
System.Console.ReadLine();

System.Console.WriteLine(">>> Kończenie pracy...");
await kpiQueue.StopProcessingAsync();
await errQueue.StopProcessingAsync();
System.Console.WriteLine(">>> Zakończono nasłuch.");


async Task KpiMessageHandler(ProcessMessageEventArgs evt)
{
   
}

async Task ErrorMsgHandler(ProcessMessageEventArgs evt)
{
   
}

Task CommonErrorHandler(ProcessErrorEventArgs evt)
{
    
}
