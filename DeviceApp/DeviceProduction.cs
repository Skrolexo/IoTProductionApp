using Azure.Communication.Email;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceApp
{
    internal class DeviceProduction
    {
        private readonly DeviceClient _client;
        private readonly string _uaDeviceName;
        private readonly OpcClient _opcClient;
        private int _statusCache = 0;

        private readonly string _mailConnStr = ResourcesDevice.EmailConnectionString;
        private readonly EmailClient _emailClient;
        private readonly string _fromEmail = ResourcesDevice.senderEmail;
        private readonly string _toEmail = ResourcesDevice.receiverEmail;

        [Flags]
        public enum DeviceStatus
        {
            None = 0,
            EmergencyStop = 1,
            PowerFailure = 2,
            SensorFailure = 4,
            Unknown = 8
        }

        public DeviceProduction(DeviceClient client, string deviceName, OpcClient opcClient)
        {
            _client = client;
            _uaDeviceName = deviceName;
            _opcClient = opcClient;
            _emailClient = new EmailClient(_mailConnStr);
        }

        public async Task InitHandlers()
        {
            await _client.SetDesiredPropertyUpdateCallbackAsync(OnTwinUpdate, _client);
            await _client.SetReceiveMessageHandlerAsync(OnCloudToDeviceMessage, _client);

            await _client.SetMethodHandlerAsync("EmergencyStop", HandleDeviceMethod, _client);
            await _client.SetMethodHandlerAsync("ResetErrorStatus", HandleDeviceMethod, _client);
            await _client.SetMethodDefaultHandlerAsync(HandleUnknownMethod, _client);

            _statusCache = await GetReportedStatusAsync();
        }

        private async Task OnTwinUpdate(TwinCollection desired, object ctx)
        {
            Console.WriteLine($"Twin changed: {JsonConvert.SerializeObject(desired)}");
            var reported = new TwinCollection();

            if (desired["EmergencyTrigger"] == 1)
            {
                var result = _opcClient.CallMethod($"ns=2;s={_uaDeviceName}", $"ns=2;s={_uaDeviceName}/EmergencyStop");
                Console.WriteLine(result != null ? "EmergencyStop success" : "EmergencyStop fail");
            }

            reported["ProductionRate"] = desired["ProductionRate"];
            _opcClient.WriteNode($"ns=2;s={_uaDeviceName}/ProductionRate", (int)desired["ProductionRate"]);
            await _client.UpdateReportedPropertiesAsync(reported).ConfigureAwait(false);
        }

        private async Task OnCloudToDeviceMessage(Message message, object _)
        {
            Console.WriteLine($"{DateTime.Now} > Received C2D message with id = {message.MessageId}");
            PrintMessage(message);
            await _client.CompleteAsync(message);
            message.Dispose();
        }

        private async Task<MethodResponse> HandleDeviceMethod(MethodRequest req, object ctx)
        {
            var result = _opcClient.CallMethod($"ns=2;s={_uaDeviceName}", $"ns=2;s={_uaDeviceName}/{req.Name}");
            Console.WriteLine($"{req.Name} executed: {(result != null ? "success" : "fail")}");
            await Task.Delay(500);
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> HandleUnknownMethod(MethodRequest req, object ctx)
        {
            Console.WriteLine($"Unknown method: {req.Name}");
            await Task.Delay(500);
            return new MethodResponse(0);
        }

    }
}
