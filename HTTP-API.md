# NAPS2 HTTP API

## Start App

```bash
# Basic (default port 9000)
dotnet run --project NAPS2.App.Mac/NAPS2.App.Mac.csproj

# Custom port
dotnet run --project NAPS2.App.Mac/NAPS2.App.Mac.csproj -- --http-port 9061

# Custom port + default profile
dotnet run --project NAPS2.App.Mac/NAPS2.App.Mac.csproj -- --http-port 9061 --profile client01

# Multiple instances (different data directories)
dotnet run --project NAPS2.App.Mac/NAPS2.App.Mac.csproj -- --http-port 9061 --profile client01 --naps2-data ~/naps2-client01
dotnet run --project NAPS2.App.Mac/NAPS2.App.Mac.csproj -- --http-port 9062 --profile client02 --naps2-data ~/naps2-client02
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/scan` | Scan with default profile (or `--profile` if set) |
| POST | `/scan/{profileName}` | Scan with specific profile |
| GET | `/profiles` | List available profiles |
| GET | `/status` | Scanner status (is_scanning, scanner_connected, device) |
| GET | `/health` | Health check |

## Status Response

```json
{
    "status": "ok",
    "is_scanning": false,
    "scanner_connected": true,
    "profile": "client01",
    "device": "fi-7160"
}
```

## Examples

```bash
# Trigger scan (uses --profile or default)
curl -X POST -H "Content-Length: 0" http://localhost:9060/scan

# Scan with specific profile
curl -X POST -H "Content-Length: 0" http://localhost:9060/scan/client02

# List profiles
curl http://localhost:9060/profiles

# Health check
curl http://localhost:9060/health
```

## Flags

| Flag | Description |
|------|-------------|
| `--http-port <port>` | HTTP API port (default: 9000) |
| `--profile <name>` | Default profile for scans |
| `--naps2-data <path>` | Custom data directory (for multiple instances) |
