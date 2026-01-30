#!/bin/bash

# Simple script to run Client Manager Server
# This server manages and monitors NAPS2 client instances

PORT=${1:-9009}

echo "Starting Client Manager Server on port $PORT..."
echo ""
echo "Available endpoints:"
echo "  GET  http://localhost:$PORT/clients"
echo "  GET  http://localhost:$PORT/clients/status"
echo "  POST http://localhost:$PORT/clients/{name}/restart"
echo ""
echo "Manages clients: client01-05 (ports 9061-9065)"
echo ""

# For now, create a simple Python server since we need to compile C#
python3 <<'PYTHON_SCRIPT'
import http.server
import socketserver
import json
import subprocess
import urllib.request
import sys
from urllib.parse import urlparse

PORT = 9009

class ClientManagerHandler(http.server.BaseHTTPRequestHandler):

    clients = [
        {"name": "client01", "port": 9061, "dataPath": "~/naps2-client01"},
        {"name": "client02", "port": 9062, "dataPath": "~/naps2-client02"},
        {"name": "client03", "port": 9063, "dataPath": "~/naps2-client03"},
        {"name": "client04", "port": 9064, "dataPath": "~/naps2-client04"},
        {"name": "client05", "port": 9065, "dataPath": "~/naps2-client05"},
    ]

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.end_headers()

    def do_GET(self):
        if self.path == '/':
            self.send_json({"message": "Client Manager Server", "version": "1.0"})
        elif self.path == '/health':
            self.send_json({"status": "ok"})
        elif self.path == '/clients':
            self.handle_get_clients()
        elif self.path == '/clients/status':
            self.handle_get_status()
        else:
            self.send_error(404, "Not found")

    def do_POST(self):
        if self.path.startswith('/clients/') and self.path.endswith('/restart'):
            parts = self.path.split('/')
            if len(parts) >= 3:
                client_name = parts[2]
                self.handle_restart(client_name)
            else:
                self.send_error(400, "Invalid request")
        else:
            self.send_error(404, "Not found")

    def handle_get_clients(self):
        result = {
            "clients": [{"name": c["name"], "port": c["port"], "dataPath": c["dataPath"]} for c in self.clients],
            "count": len(self.clients)
        }
        self.send_json(result)

    def handle_get_status(self):
        statuses = []
        for client in self.clients:
            health = self.check_health(client["port"])
            statuses.append({
                "name": client["name"],
                "port": client["port"],
                "healthy": health["healthy"],
                "status": health["status"]
            })

        healthy_count = sum(1 for s in statuses if s["healthy"])
        result = {
            "clients": statuses,
            "summary": {
                "total": len(self.clients),
                "healthy": healthy_count,
                "unhealthy": len(self.clients) - healthy_count
            }
        }
        self.send_json(result)

    def handle_restart(self, client_name):
        client = next((c for c in self.clients if c["name"] == client_name), None)
        if not client:
            self.send_error(404, f"Client '{client_name}' not found")
            return

        print(f"Restarting {client_name}...")

        import os
        import signal
        import time

        # Kill all NAPS2 processes for this port using pkill
        try:
            subprocess.run(
                f"pkill -9 -f 'NAPS2.*--http-port {client['port']}'",
                shell=True,
                stderr=subprocess.DEVNULL
            )
            print(f"Sent kill signal to processes using port {client['port']}")
        except Exception as e:
            print(f"Error sending kill signal: {e}")

        # Wait for port to be completely released (up to 10 seconds)
        max_wait = 10
        wait_interval = 0.5
        for i in range(int(max_wait / wait_interval)):
            try:
                # Check if port is still in use
                result = subprocess.run(
                    f"lsof -ti :{client['port']}",
                    shell=True,
                    capture_output=True,
                    text=True
                )
                if result.returncode != 0 or not result.stdout.strip():
                    # Port is free
                    print(f"Port {client['port']} is now free")
                    break
                else:
                    print(f"Waiting for port {client['port']} to be released... ({i+1})")
                    time.sleep(wait_interval)
            except Exception as e:
                print(f"Error checking port: {e}")
                break
        else:
            print(f"Warning: Port {client['port']} may still be in use")

        # Extra safety wait
        time.sleep(1)

        # Start new process
        bin_path = "/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2"
        data_path = client["dataPath"].replace("~", subprocess.os.path.expanduser("~"))
        cmd = [bin_path, "--http-port", str(client["port"]), "--profile", client["name"], "--naps2-data", data_path]

        subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        print(f"Started {client_name}")

        # Wait and check health
        time.sleep(3)
        health = self.check_health(client["port"])

        result = {
            "success": health["healthy"],
            "message": f"Client {client_name} restarted",
            "client": client_name,
            "port": client["port"],
            "healthy": health["healthy"],
            "status": health["status"]
        }
        self.send_json(result)

    def check_health(self, port):
        try:
            response = urllib.request.urlopen(f"http://localhost:{port}/health", timeout=2)
            if response.status == 200:
                return {"healthy": True, "status": "running"}
            else:
                return {"healthy": False, "status": "unhealthy"}
        except:
            return {"healthy": False, "status": "not_responding"}

    def send_json(self, data):
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps(data, indent=2).encode())

    def log_message(self, format, *args):
        print(f"{self.address_string()} - {format % args}")

socketserver.TCPServer.allow_reuse_address = True
try:
    with socketserver.TCPServer(("", PORT), ClientManagerHandler) as httpd:
        print(f"Client Manager Server running on http://localhost:{PORT}")
        print("Press Ctrl+C to stop")
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            print("\\nShutting down...")
except OSError as e:
    if e.errno == 48:  # Address already in use
        print(f"\\nError: Port {PORT} is already in use!")
        print("Please stop the existing process or use a different port.")
        print(f"\\nTo find and kill the process using port {PORT}:")
        print(f"  lsof -i :{PORT}")
        print(f"  kill -9 <PID>")
        sys.exit(1)
    else:
        raise
PYTHON_SCRIPT
