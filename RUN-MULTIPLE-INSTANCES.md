# วิธีรัน NAPS2 หลาย Instance พร้อมกัน

## Client Manager Server (แนะนำ)

ตอนนี้มี **Client Manager Server** สำหรับจัดการและตรวจสอบสถานะของ client ทั้งหมด!

### รัน Manager Server

```bash
./run-manager.sh
```

Manager จะรันที่ port 9009 (default) และจัดการ clients ทั้ง 5 ตัว (client01-05)

### Manager API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | ตรวจสอบว่า manager server ทำงานอยู่ |
| `/clients` | GET | ดูรายการ clients ทั้งหมด |
| `/clients/status` | GET | ตรวจสอบ health status ของ clients ทั้งหมด |
| `/clients/{name}/restart` | POST | Restart client ที่ระบุ (เช่น client01) |

### ตัวอย่างการใช้งาน Manager

```bash
# ดูรายการ clients ทั้งหมด
curl http://localhost:9009/clients

# ตรวจสอบสถานะทั้งหมด
curl http://localhost:9009/clients/status

# Restart client01
curl -X POST http://localhost:9009/clients/client01/restart

# Restart client02
curl -X POST http://localhost:9009/clients/client02/restart
```

---

## วิธีรัน Clients แบบ Manual (5 Instances)

### ขั้นตอนที่ 1: Build โปรเจค (ครั้งแรกเท่านั้น)

```bash
dotnet build NAPS2.App.Mac/NAPS2.App.Mac.csproj
```

### ขั้นตอนที่ 2: รัน 5 Instances พร้อมกัน

เปิด 5 terminals แยกกัน หรือรันใน background:

```bash
# Client 01 - Port 9061
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01 &

# Client 02 - Port 9062
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9062 --profile client02 --naps2-data ~/naps2-client02 &

# Client 03 - Port 9063
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9063 --profile client03 --naps2-data ~/naps2-client03 &

# Client 04 - Port 9064
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9064 --profile client04 --naps2-data ~/naps2-client04 &

# Client 05 - Port 9065
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9065 --profile client05 --naps2-data ~/naps2-client05 &
```

### ขั้นตอนที่ 3: ตรวจสอบสถานะ

```bash
# ตรวจสอบว่า instance ไหนทำงานอยู่
curl http://localhost:9061/health
curl http://localhost:9062/health
curl http://localhost:9063/health
curl http://localhost:9064/health
curl http://localhost:9065/health

# ตรวจสอบสถานะแบบละเอียด
curl http://localhost:9061/status
```

## รันทีละ Instance

ถ้าต้องการรันทีละ instance:

```bash
# Client 01
/Users/piyawongmahattanasawat/Desktop/roll-v2/naps2/NAPS2.App.Mac/bin/Debug/net9-macos/NAPS2.app/Contents/MacOS/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01
```

## ตรวจสอบ Process ที่รันอยู่

```bash
# ดู process ทั้งหมด
ps aux | grep NAPS2 | grep http-port

# นับจำนวน instance
ps aux | grep NAPS2 | grep http-port | wc -l
```

## หยุด Instance

```bash
# หาก PID ของแต่ละ instance
ps aux | grep "NAPS2.*9061" | grep -v grep

# หยุด instance (แทน PID ด้วยเลข PID ที่เจอ)
kill <PID>

# หยุดทุก instance พร้อมกัน
pkill -f "NAPS2.*http-port"
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | ตรวจสอบว่า server ทำงานอยู่ |
| `/status` | GET | ดูสถานะการสแกน, scanner, profile |
| `/scan` | POST | สแกนด้วย profile default |
| `/scan/{profileName}` | POST | สแกนด้วย profile ที่ระบุ |
| `/profiles` | GET | ดูรายการ profiles |

## ตัวอย่างการใช้งาน

```bash
# Trigger scan
curl -X POST -H "Content-Length: 0" http://localhost:9061/scan

# Scan with specific profile
curl -X POST -H "Content-Length: 0" http://localhost:9061/scan/client02

# List profiles
curl http://localhost:9061/profiles

# Check status
curl http://localhost:9061/status
```

## หมายเหตุ

- แต่ละ instance จะใช้ data directory แยกกัน (`~/naps2-client01`, `~/naps2-client02`, ฯลฯ)
- แต่ละ instance จะใช้ profile แยกกัน (client01, client02, client03, client04, client05)
- Client Ports: 9061-9065 (5 clients)
- Manager Port: 9009 (Client Manager Server)
- ถ้า instance หยุดทำงาน ใช้ Manager Server เพื่อ restart หรือรัน command ใหม่อีกครั้ง
