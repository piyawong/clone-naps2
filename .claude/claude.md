# NAPS2 Project Notes

## Build Configuration

### Build Targets

- **Test Build**: `/tmp/naps2-build`
- **Debug Build**: `NAPS2.App.Mac/bin/Debug/net9-macos/`
- **Production Build (Release)**: `NAPS2.App.Mac/bin/Release/net9-macos/` ⭐ **ใช้อันนี้สำหรับ production**

### Build Commands

```bash
# Test build (สำหรับทดสอบ - ไม่กระทบ production binary)
dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj -o /tmp/naps2-build

# Debug build
dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj

# Production build (Release)
dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj -c Release
```

### Important Rules

⚠️ **เมื่อผู้ใช้สั่ง "build"** → ต้องถามก่อนว่า **test หรือ prod**

- ถ้าบอก **"build test"** → build ไปที่ `/tmp/naps2-build` เลย (ไม่ต้องถาม)
- ถ้าบอก **"build prod"** → build แบบ Release configuration
- ถ้าบอก **"build"** อย่างเดียว → ถามก่อนว่า test หรือ prod

## Client Configuration (Production)

### Current Binary Path (PROD)
```
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2
```

### Running Clients (01-05)

```bash
# Client 01 - Port 9061
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01 &

# Client 02 - Port 9062
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9062 --profile client02 --naps2-data ~/naps2-client02 &

# Client 03 - Port 9063
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9063 --profile client03 --naps2-data ~/naps2-client03 &

# Client 04 - Port 9064
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9064 --profile client04 --naps2-data ~/naps2-client04 &

# Client 05 - Port 9065
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9065 --profile client05 --naps2-data ~/naps2-client05 &
```

### Client Ports & Data Directories

| Client | Port | Profile | Data Directory |
|--------|------|---------|----------------|
| client01 | 9061 | client01 | ~/naps2-client01 |
| client02 | 9062 | client02 | ~/naps2-client02 |
| client03 | 9063 | client03 | ~/naps2-client03 |
| client04 | 9064 | client04 | ~/naps2-client04 |
| client05 | 9065 | client05 | ~/naps2-client05 |

### Quick Restart Commands

```bash
# Restart all clients (01-04)
ps aux | grep "NAPS2.*--http-port 906[1-4]" | grep -v grep | awk '{print $2}' | xargs kill -9 2>/dev/null
sleep 2
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01 &
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9062 --profile client02 --naps2-data ~/naps2-client02 &
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9063 --profile client03 --naps2-data ~/naps2-client03 &
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9064 --profile client04 --naps2-data ~/naps2-client04 &

# Restart single client (example: client01)
ps aux | grep "NAPS2.*9061" | grep -v grep | awk '{print $2}' | xargs kill -9 2>/dev/null
sleep 2
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Release/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01 &
```

### Health Check Commands

```bash
# Check all clients
for port in 9061 9062 9063 9064 9065; do
  echo -n "Client (port $port): "
  curl -s http://localhost:$port/health || echo "FAIL"
done

# Check single client
curl -s http://localhost:9061/health
curl -s http://localhost:9061/status
```
