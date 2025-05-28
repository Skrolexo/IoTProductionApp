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


        using var clientOpc = new OpcClient(ResourcesDevice.opcClientURL);
        clientOpc.Connect();

        var availableMachines = FetchAvailableMachines(clientOpc, iotConnections);
        var machineAgents = new List<DeviceProduction>();


        for (int i = 0; i < availableMachines.Count; i++)
        {
            var iotClient = DeviceClient.CreateFromConnectionString(iotConnections[i], Microsoft.Azure.Devices.Client.TransportType.Mqtt);
            await iotClient.OpenAsync();

            var device = new DeviceProduction(iotClient, availableMachines[i].DisplayName, clientOpc);
            await device.InitHandlers();

            machineAgents.Add(device);

            System.Console.WriteLine($"\n[INFO] {availableMachines[i].DisplayName} is now registered in IoT Hub as agent.");
        }

        while (true)
        {
            if (index >= availableMachines.Count)
                index = 0;

            string currentName = availableMachines[index].DisplayName;

            var dataNodes = new[]
            {
                new OpcReadNode($"ns=2;s={currentName}/ProductionStatus", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/ProductionStatus"),
                new OpcReadNode($"ns=2;s={currentName}/ProductionRate", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/ProductionRate"),
                new OpcReadNode($"ns=2;s={currentName}/WorkorderId", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/WorkorderId"),
                new OpcReadNode($"ns=2;s={currentName}/Temperature", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/Temperature"),
                new OpcReadNode($"ns=2;s={currentName}/GoodCount", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/GoodCount"),
                new OpcReadNode($"ns=2;s={currentName}/BadCount", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/BadCount"),
                new OpcReadNode($"ns=2;s={currentName}/DeviceError", OpcAttribute.DisplayName),
                new OpcReadNode($"ns=2;s={currentName}/DeviceError"),
            };

            var machineReadings = clientOpc.ReadNodes(dataNodes);
            machineAgents[index].HandleOpcData(machineReadings);

            System.Console.WriteLine($"\nResult for {availableMachines[index].DisplayName}:");
            foreach (var node in machineReadings)
                System.Console.WriteLine(node.Value);

            index++;
            await Task.Delay(3000);
        }
    }

    private static List<OpcNodeInfo> FetchAvailableMachines(OpcClient client, List<string> connectionStrings)
    {
        var foundDevices = ListDevices(client);
        if (foundDevices.Count == 0)
            throw new ArgumentException("No machines detected on OPC UA server.");

        if (foundDevices.Count > connectionStrings.Count)
        {
            int missing = foundDevices.Count - connectionStrings.Count;
            throw new ArgumentException($"Not enough IoT connection strings! Add {missing} more to your resources.");
        }

        System.Console.WriteLine("Using provided IoT connection strings.");
        return foundDevices;
    }

    private static List<OpcNodeInfo> ListDevices(OpcClient client)
    {
        var root = client.BrowseNode(OpcObjectTypes.ObjectsFolder);
        var result = new List<OpcNodeInfo>();
        foreach (var item in root.Children())
        {
            if (IsRecognizedDevice(item))
                result.Add(item);
        }
        return result;
    }

    private static bool IsRecognizedDevice(OpcNodeInfo node)
    {
        var pattern = @"^Device [0-9]+$";
        var regex = new Regex(pattern);
        var displayName = node.Attribute(OpcAttribute.DisplayName).Value?.ToString() ?? "";
        return regex.IsMatch(displayName);
    }
}
