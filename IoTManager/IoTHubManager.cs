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
    }
}
