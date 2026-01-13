# NAPS2 Project Notes

## Build Configuration

### Build Targets

- **Test Build**: `/tmp/naps2-build`
- **Production Build**: `NAPS2.App.Mac/bin/Debug/net9-macos/` (default)

### Build Commands

```bash
# Test build (สำหรับทดสอบ - ไม่กระทบ production binary)
dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj -o /tmp/naps2-build

# Production build (ลงที่เดิม)
dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj
```

### Important Rules

⚠️ **เมื่อผู้ใช้สั่ง "build"** → ต้องถามก่อนว่า **test หรือ prod**

- ถ้าบอก **"build test"** → build ไปที่ `/tmp/naps2-build` เลย (ไม่ต้องถาม)
- ถ้าบอก **"build"** อย่างเดียว → ถามก่อนว่า test หรือ prod

### Running Test Build

```bash
# Run client from test build
/tmp/naps2-build/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01
```

### Running Production Build

```bash
# Run client from production build
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01
```
