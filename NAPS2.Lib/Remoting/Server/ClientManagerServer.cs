using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NAPS2.Remoting.Server;

/// <summary>
/// HTTP server for managing multiple NAPS2 client instances
/// </summary>
public class ClientManagerServer : IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpListener _listener;
    private readonly List<ClientInfo> _clients;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    public int Port { get; }
    public bool IsRunning => _listenTask != null && !_listenTask.IsCompleted;

    public ClientManagerServer(ILogger logger, int port = 9009)
    {
        _logger = logger;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add($"http://+:{port}/");

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Initialize client configurations
        _clients = new List<ClientInfo>
        {
            new ClientInfo("client01", 9061, "~/naps2-client01"),
            new ClientInfo("client02", 9062, "~/naps2-client02"),
            new ClientInfo("client03", 9063, "~/naps2-client03"),
            new ClientInfo("client04", 9064, "~/naps2-client04"),
            new ClientInfo("client05", 9065, "~/naps2-client05")
        };
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            _listenTask = ListenAsync(_cts.Token);
            _logger.LogInformation("ClientManagerServer started on http://localhost:{Port}/", Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ClientManagerServer on port {Port}", Port);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _listener.Stop();

        if (_listenTask != null)
        {
            try
            {
                await _listenTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _logger.LogInformation("ClientManagerServer stopped");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = HandleRequestAsync(context);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting HTTP connection");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Enable CORS
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        try
        {
            _logger.LogDebug("HTTP {Method} {Path}", request.HttpMethod, request.Url?.AbsolutePath);

            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            // Handle OPTIONS for CORS
            if (method == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            object result = (method, path) switch
            {
                ("GET", "/") => new { message = "Client Manager Server", version = "1.0" },
                ("GET", "/health") => new { status = "ok" },
                ("GET", "/clients") => await HandleGetClients(),
                ("GET", "/clients/status") => await HandleGetAllStatus(),
                ("POST", var p) when p.StartsWith("/clients/") && p.EndsWith("/restart")
                    => await HandleRestartClient(ExtractClientName(p)),
                _ => throw new HttpException(404, "Not found")
            };

            await SendJsonResponse(response, 200, result);
        }
        catch (HttpException ex)
        {
            await SendJsonResponse(response, ex.StatusCode, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP request");
            await SendJsonResponse(response, 500, new { error = "Internal server error", details = ex.Message });
        }
        finally
        {
            response.Close();
        }
    }

    private string ExtractClientName(string path)
    {
        // Extract client name from path like /clients/client01/restart
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return parts[1];
        }
        throw new HttpException(400, "Invalid client name");
    }

    private async Task<object> HandleGetClients()
    {
        var clientList = _clients.Select(c => new
        {
            name = c.Name,
            port = c.Port,
            dataPath = c.DataPath
        }).ToList();

        return new { clients = clientList, count = clientList.Count };
    }

    private async Task<object> HandleGetAllStatus()
    {
        var statusTasks = _clients.Select(async client =>
        {
            var health = await CheckClientHealth(client);
            return new
            {
                name = client.Name,
                port = client.Port,
                healthy = health.IsHealthy,
                status = health.Status,
                details = health.Details
            };
        });

        var statuses = await Task.WhenAll(statusTasks);
        var healthyCount = statuses.Count(s => s.healthy);

        return new
        {
            clients = statuses,
            summary = new
            {
                total = _clients.Count,
                healthy = healthyCount,
                unhealthy = _clients.Count - healthyCount
            }
        };
    }

    private async Task<object> HandleRestartClient(string clientName)
    {
        var client = _clients.FirstOrDefault(c => c.Name.Equals(clientName, StringComparison.OrdinalIgnoreCase));
        if (client == null)
        {
            throw new HttpException(404, $"Client '{clientName}' not found");
        }

        _logger.LogInformation("Restarting client {ClientName}...", clientName);

        try
        {
            // Find and kill the process
            await KillClientProcess(client);

            // Wait a bit for the port to be released
            await Task.Delay(2000);

            // Start new process
            await StartClientProcess(client);

            // Wait for it to start
            await Task.Delay(3000);

            // Check if it's healthy
            var health = await CheckClientHealth(client);

            return new
            {
                success = health.IsHealthy,
                message = health.IsHealthy ? $"Client {clientName} restarted successfully" : "Client restarted but not healthy",
                client = clientName,
                port = client.Port,
                healthy = health.IsHealthy,
                status = health.Status
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting client {ClientName}", clientName);
            return new
            {
                success = false,
                message = $"Failed to restart client: {ex.Message}",
                client = clientName
            };
        }
    }

    private async Task<HealthStatus> CheckClientHealth(ClientInfo client)
    {
        try
        {
            var response = await _httpClient.GetAsync($"http://localhost:{client.Port}/health");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return new HealthStatus
                {
                    IsHealthy = true,
                    Status = "running",
                    Details = content
                };
            }
            else
            {
                return new HealthStatus
                {
                    IsHealthy = false,
                    Status = "unhealthy",
                    Details = $"HTTP {response.StatusCode}"
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Status = "not_responding",
                Details = ex.Message
            };
        }
        catch (TaskCanceledException)
        {
            return new HealthStatus
            {
                IsHealthy = false,
                Status = "timeout",
                Details = "Health check timed out"
            };
        }
    }

    private async Task KillClientProcess(ClientInfo client)
    {
        // Find process by port
        var psi = new ProcessStartInfo
        {
            FileName = "sh",
            Arguments = $"-c \"ps aux | grep '[N]APS2.*{client.Port}' | awk '{{print $2}}'\"",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var pids = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pid in pids)
            {
                if (int.TryParse(pid.Trim(), out var processId))
                {
                    try
                    {
                        Process.GetProcessById(processId).Kill(true);
                        _logger.LogInformation("Killed process {PID} for {ClientName}", processId, client.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill process {PID}", processId);
                    }
                }
            }
        }
    }

    private async Task StartClientProcess(ClientInfo client)
    {
        var binPath = "/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2";
        var dataPath = client.DataPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        var psi = new ProcessStartInfo
        {
            FileName = binPath,
            Arguments = $"--http-port {client.Port} --profile {client.Name} --naps2-data {dataPath}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(psi);
        if (process == null)
        {
            throw new Exception($"Failed to start process for {client.Name}");
        }

        _logger.LogInformation("Started new process for {ClientName} (PID: {PID})", client.Name, process.Id);
    }

    private static async Task SendJsonResponse(HttpListenerResponse response, int statusCode, object data)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
        var buffer = Encoding.UTF8.GetBytes(json);

        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _listener.Close();
        _httpClient.Dispose();
        _cts?.Dispose();
    }

    private class ClientInfo
    {
        public string Name { get; }
        public int Port { get; }
        public string DataPath { get; }

        public ClientInfo(string name, int port, string dataPath)
        {
            Name = name;
            Port = port;
            DataPath = dataPath;
        }
    }

    private class HealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = "";
        public string Details { get; set; } = "";
    }

    private class HttpException : Exception
    {
        public int StatusCode { get; }
        public HttpException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
