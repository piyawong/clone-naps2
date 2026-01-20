using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using NAPS2.Config;
using NAPS2.EtoForms.Desktop;
using NAPS2.Platform;

namespace NAPS2.Remoting.Server;

/// <summary>
/// Simple HTTP server that allows triggering scans via HTTP requests.
/// </summary>
public class ScanHttpServer : IDisposable
{
    private readonly ILogger _logger;
    private readonly IDesktopScanController _scanController;
    private readonly IProfileManager _profileManager;
    private readonly HttpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    public int Port { get; }
    public bool IsRunning => _listenTask != null && !_listenTask.IsCompleted;

    public ScanHttpServer(ILogger logger, IDesktopScanController scanController, IProfileManager profileManager, int port = 9000)
    {
        _logger = logger;
        _scanController = scanController;
        _profileManager = profileManager;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _listener.Start();
            _listenTask = ListenAsync(_cts.Token);
            _logger.LogInformation("ScanHttpServer started on http://localhost:{Port}/", Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ScanHttpServer on port {Port}", Port);
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

        _logger.LogInformation("ScanHttpServer stopped");
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

        try
        {
            _logger.LogDebug("HTTP {Method} {Path}", request.HttpMethod, request.Url?.AbsolutePath);

            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            object result = (method, path) switch
            {
                ("POST", "/scan") => await HandleScan(null),
                ("POST", "/scan/default") => await HandleScan(null),
                ("GET", "/profiles") => HandleGetProfiles(),
                ("GET", "/status") => HandleGetStatus(),
                ("GET", "/health") => new { status = "ok" },
                _ when method == "POST" && path.StartsWith("/scan/") => await HandleScan(path.Substring(6)),
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
            await SendJsonResponse(response, 500, new { error = "Internal server error" });
        }
        finally
        {
            response.Close();
        }
    }

    private Task<object> HandleScan(string? profileName)
    {
        // URL decode the profile name
        if (profileName != null)
        {
            profileName = Uri.UnescapeDataString(profileName);
        }

        // Use command line --profile override if no profile specified in request
        if (string.IsNullOrEmpty(profileName) && !string.IsNullOrEmpty(ApplicationLifecycle.ProfileOverride))
        {
            profileName = ApplicationLifecycle.ProfileOverride;
        }

        _logger.LogInformation("Scan triggered via HTTP with profile: {Profile}", profileName ?? "default");

        // Reload profiles from disk to get latest settings
        _profileManager.Reload();

        // Find profile by name if specified
        Scan.ScanProfile? profile = null;
        if (!string.IsNullOrEmpty(profileName))
        {
            profile = _profileManager.Profiles.FirstOrDefault(p =>
                p.DisplayName.Equals(profileName, StringComparison.OrdinalIgnoreCase));

            if (profile == null)
            {
                throw new HttpException(404, $"Profile '{profileName}' not found");
            }
        }

        // Run scan on UI thread
        Invoker.Current.InvokeDispatch(async () =>
        {
            if (profile != null)
            {
                await _scanController.ScanWithProfile(profile);
            }
            else
            {
                await _scanController.ScanDefault();
            }
        });

        return Task.FromResult<object>(new
        {
            success = true,
            message = "Scan triggered",
            profile = profile?.DisplayName ?? "default"
        });
    }

    private object HandleGetProfiles()
    {
        var profiles = _profileManager.Profiles.Select(p => new
        {
            name = p.DisplayName,
            isDefault = p == _profileManager.DefaultProfile
        }).ToList();

        return new { profiles };
    }

    private object HandleGetStatus()
    {
        // Get the profile to check (command line override or default)
        var profileName = ApplicationLifecycle.ProfileOverride;
        Scan.ScanProfile? profile = null;

        if (!string.IsNullOrEmpty(profileName))
        {
            profile = _profileManager.Profiles.FirstOrDefault(p =>
                p.DisplayName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        }
        profile ??= _profileManager.DefaultProfile;

        // Check if scanner is configured (has a device set)
        var scannerConnected = profile?.Device != null;

        return new
        {
            status = "ok",
            is_scanning = _scanController.IsScanning,
            scanner_connected = scannerConnected,
            profile = profile?.DisplayName ?? "none",
            device = profile?.Device?.Name ?? "none"
        };
    }

    private static async Task SendJsonResponse(HttpListenerResponse response, int statusCode, object data)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(data);
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
        _cts?.Dispose();
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
