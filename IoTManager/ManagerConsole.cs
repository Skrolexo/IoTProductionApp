using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using Newtonsoft.Json.Linq;
using IoTManager;


var iotHubConn = ResourcesManager.iotHubConnectionString;
var serviceBusConn = ResourcesManager.serviceBusConnectionString;

using var devRegistry = RegistryManager.CreateFromConnectionString(iotHubConn);

var iotHubManager = new IoTHubManager(devRegistry);

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
    System.Console.WriteLine($"[KPI QUEUE] Nowa wiadomość:\n\t{evt.Message.Body}");

    var kpiMsg = JObject.Parse(evt.Message.Body.ToString());
    string devId = kpiMsg["ConnectionDeviceId"].ToString();
    double kpiVal = kpiMsg["KPI"].Value<double>();

    if (kpiVal < 90)
        await iotHubManager.LowerProductionRateAsync(devId);

    await evt.CompleteMessageAsync(evt.Message);
}

async Task ErrorMsgHandler(ProcessMessageEventArgs evt)
{
    System.Console.WriteLine($"[ERROR QUEUE] Nowa wiadomość:\n\t{evt.Message.Body}");

    var errMsg = JObject.Parse(evt.Message.Body.ToString());
    string devId = errMsg["ConnectionDeviceId"].ToString();
    int sumErr = errMsg["sumErrors"].Value<int>();

    await iotHubManager.SetEmergencyFlagAsync(devId, sumErr);

    await evt.CompleteMessageAsync(evt.Message);
}

Task CommonErrorHandler(ProcessErrorEventArgs evt)
{
    System.Console.WriteLine("[!] ServiceBus BŁĄD: " + evt.Exception.Message);
    return Task.CompletedTask;
}
