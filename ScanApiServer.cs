using NAPS2.Escl.Server;
using NAPS2.Images.Mac;
using NAPS2.Remoting.Server;
using NAPS2.Scan;

namespace NAPS2.Examples;

/// <summary>
/// Example: Run NAPS2 as an API server that can receive scan requests via HTTP
/// </summary>
public class ScanApiServer
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting NAPS2 Scan API Server...");

        // Initialize scanning context
        using var scanningContext = new ScanningContext(new MacImageContext());

        // Get available scanners
        var controller = new ScanController(scanningContext);
        var devices = await controller.GetDeviceList();

        if (!devices.Any())
        {
            Console.WriteLine("No scanners found!");
            return;
        }

        Console.WriteLine($"Found {devices.Count} scanner(s):");
        foreach (var device in devices)
        {
            Console.WriteLine($"  - {device.Name} ({device.ID})");
        }

        // Set up ESCL API server
        using var scanServer = new ScanServer(scanningContext, new EsclServer());

        // Register all scanners
        int port = 8080;
        foreach (var device in devices)
        {
            scanServer.RegisterDevice(device, displayName: device.Name, port: port);
            Console.WriteLine($"Registered '{device.Name}' on http://localhost:{port}/eSCL/");
            port++;
        }

        // Start server
        await scanServer.Start();
        Console.WriteLine("\n✅ NAPS2 API Server is running!");
        Console.WriteLine("\nAPI Endpoints:");
        Console.WriteLine("  GET  /eSCL/ScannerCapabilities - Get scanner info");
        Console.WriteLine("  GET  /eSCL/ScannerStatus - Get current status");
        Console.WriteLine("  POST /eSCL/ScanJobs - Start a scan job");
        Console.WriteLine("  GET  /eSCL/ScanJobs/{id}/NextDocument - Get scanned image");
        Console.WriteLine("\nPress Enter to stop server...");

        Console.ReadLine();

        await scanServer.Stop();
        Console.WriteLine("Server stopped.");
    }
}
