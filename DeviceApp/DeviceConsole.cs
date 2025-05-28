using System.Text.RegularExpressions;
using Microsoft.Azure.Devices.Client;
using Opc.UaFx;
using Opc.UaFx.Client;
using DeviceApp;
using Microsoft.Azure.Devices;
using System.Net.Sockets;
using System.Runtime.InteropServices;

public class DeviceConsole
{
    public static async Task Main(string[] args)
    {
        int index = 0;

        var iotConnections = new List<string>
        {
 
            ResourcesDevice.Device1ConnectionString,
            ResourcesDevice.Device2ConnectionString,
            ResourcesDevice.Device3ConnectionString
        };
    }
}
