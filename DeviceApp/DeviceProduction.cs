using Azure;
using Azure.Communication.Email;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
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
        private void PrintMessage(Message message)
        {
            var body = Encoding.ASCII.GetString(message.GetBytes());
            Console.WriteLine($"Received message: {body}");
            int i = 0;
            foreach (var prop in message.Properties)
            {
                Console.WriteLine($"Property {i++}: {prop.Key} = {prop.Value}");
            }
        }

        public async Task UpdateTwinAsync(IEnumerable<OpcValue> data)
        {
            var reportedProps = new TwinCollection();
            reportedProps["ProductionRate"] = data.ElementAt(3).Value;

            var deviceStatus = (int)data.ElementAt(13).Value;
            if (deviceStatus != _statusCache)
            {
                NotifyNewError(_statusCache, deviceStatus);
                NotifyStatusChange(_statusCache, deviceStatus);
                _statusCache = deviceStatus;

                var flags = Enum.GetValues(typeof(DeviceStatus))
                    .Cast<DeviceStatus>()
                    .Where(f => f != DeviceStatus.None && ((DeviceStatus)deviceStatus).HasFlag(f))
                    .Select(f => f.ToString())
                    .ToList();

                reportedProps["DeviceStatus"] = new JArray(flags);
            }
            await _client.UpdateReportedPropertiesAsync(reportedProps);
        }

        public async Task<int> GetReportedStatusAsync()
        {
            var twin = await _client.GetTwinAsync();
            var reported = twin.Properties.Reported;

            if (reported.Contains("DeviceStatus"))
            {
                var array = reported["DeviceStatus"] as JArray;
                var list = array?.ToObject<List<string>>() ?? new List<string>();
                int statusNum = 0;
                foreach (var s in list)
                {
                    if (Enum.TryParse<DeviceStatus>(s, out var flag))
                        statusNum |= (int)flag;
                }
                return statusNum;
            }
            return 0;
        }

        private async void NotifyStatusChange(int oldStatus, int newStatus)
        {
            var msg = new
            {
                Message = $"Device status update: {(DeviceStatus)oldStatus} → {(DeviceStatus)newStatus}"
            };
            await SendMessage(msg);
        }

        private async void NotifyNewError(int oldStatus, int newStatus)
        {
            int newlyAdded = newStatus & ~oldStatus;
            var errorList = Enum.GetValues(typeof(DeviceStatus))
                .Cast<DeviceStatus>()
                .Where(flag => (newlyAdded & (int)flag) != 0)
                .Select(flag => flag.ToString())
                .ToArray();

            if (errorList.Length > 0)
            {
                await SendMailAsync(string.Join(", ", errorList));
            }

            var data = new { NewError = errorList.Length };
            await SendMessage(data);
        }
        private async void SendTelemetry(IEnumerable<OpcValue> data)
        {
            var payload = new
            {
                ProductionStatus = data.ElementAt(1).Value,
                WorkerId = data.ElementAt(5).Value,
                Temperature = data.ElementAt(7).Value,
                GoodCount = data.ElementAt(9).Value,
                BadCount = data.ElementAt(11).Value
            };
            await SendMessage(payload);
        }

        public async Task SendMessage(object data)
        {
            var json = JsonConvert.SerializeObject(data);
            var msg = new Message(Encoding.UTF8.GetBytes(json))
            {
                ContentType = MediaTypeNames.Application.Json,
                ContentEncoding = "utf-8"
            };
            await _client.SendEventAsync(msg);
        }
        private async Task SendMailAsync(string errorDesc)
        {
            try
            {
                var subject = $"Error occurred on {_uaDeviceName}";
                var body = $"Errors detected: {errorDesc}";
                var content = new EmailContent(subject) { PlainText = body };
                var emailMsg = new EmailMessage(_fromEmail, _toEmail, content);

                await _emailClient.SendAsync(Azure.WaitUntil.Completed, emailMsg);
                Console.WriteLine($"Email sent: {errorDesc}");
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine("Email failed: " + ex.Message);
            }
        }

    }
}
