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
    }
}
