using Microsoft.Azure.Devices;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace IoTManager
{
    internal class IoTHubManager
    {
        private readonly RegistryManager _deviceRegistry;

        public IoTHubManager(RegistryManager regManager)
        {
            _deviceRegistry = regManager;
        }

        public async Task LowerProductionRateAsync(string deviceId)
        {
            var twin = await _deviceRegistry.GetTwinAsync(deviceId);

            if (twin == null)
            {
                Console.WriteLine($"[WARN] Device Twin for '{deviceId}' not found!");
                return;
            }

            var reportedRateObj = twin.Properties.Reported["ProductionRate"];
            if (reportedRateObj == null)
            {
                Console.WriteLine($"[WARN] Reported ProductionRate not set for '{deviceId}'!");
                return;
            }

            var reportedRate = (int)reportedRateObj;
            if (reportedRate >= 10)
            {
                twin.Properties.Desired["ProductionRate"] = reportedRate - 10;
                await _deviceRegistry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
            }
        }


        public async Task SetEmergencyFlagAsync(string deviceId, int errorCount)
        {
            var twin = await _deviceRegistry.GetTwinAsync(deviceId);
            twin.Properties.Desired["EmergencyTrigger"] = (errorCount >= 3) ? 1 : 0;
            await _deviceRegistry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
        }
    }
}
