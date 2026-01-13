using Microsoft.Extensions.Logging;
using NAPS2.Remoting.Server;

namespace NAPS2;

/// <summary>
/// Standalone runner for Client Manager Server
/// Usage: dotnet run --project NAPS2.App.Mac ClientManagerRunner.cs [port]
/// </summary>
public class ClientManagerRunner
{
    public static async Task Main(string[] args)
    {
        // Parse port from command line args (default: 9009)
        var port = 9009;
        if (args.Length > 0 && int.TryParse(args[0], out var parsedPort))
        {
            port = parsedPort;
        }

        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<ClientManagerServer>();

        // Create and start server
        using var server = new ClientManagerServer(logger, port);

        Console.WriteLine($"Starting Client Manager Server on port {port}...");
        Console.WriteLine("This server manages NAPS2 clients on ports 9061-9065");
        Console.WriteLine();
        Console.WriteLine("Available endpoints:");
        Console.WriteLine($"  GET  http://localhost:{port}/clients          - List all clients");
        Console.WriteLine($"  GET  http://localhost:{port}/clients/status   - Get health status of all clients");
        Console.WriteLine($"  POST http://localhost:{port}/clients/{{name}}/restart - Restart a specific client");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop...");
        Console.WriteLine();

        server.Start();

        // Wait for Ctrl+C
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\nShutting down...");
        }

        await server.StopAsync();
        Console.WriteLine("Server stopped.");
    }
}
