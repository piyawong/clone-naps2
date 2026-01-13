# Client Manager API

Manager Server รันที่ **Port 9009** และใช้จัดการ NAPS2 clients ทั้ง 5 ตัว (client01-05)

## เริ่มต้น Manager Server

```bash
./run-manager.sh
```

---

## API Endpoints

### 1. Health Check

ตรวจสอบว่า Manager Server ทำงานอยู่

**Endpoint:** `GET /health`

**ตัวอย่าง:**
```bash
curl http://localhost:9009/health
```

**Response:**
```json
{
  "status": "ok"
}
```

---

### 2. ดูรายการ Clients ทั้งหมด

ดูรายการ clients ที่ Manager จัดการ

**Endpoint:** `GET /clients`

**ตัวอย่าง:**
```bash
curl http://localhost:9009/clients
```

**Response:**
```json
{
  "clients": [
    {
      "name": "client01",
      "port": 9061,
      "dataPath": "~/naps2-client01"
    },
    {
      "name": "client02",
      "port": 9062,
      "dataPath": "~/naps2-client02"
    },
    {
      "name": "client03",
      "port": 9063,
      "dataPath": "~/naps2-client03"
    },
    {
      "name": "client04",
      "port": 9064,
      "dataPath": "~/naps2-client04"
    },
    {
      "name": "client05",
      "port": 9065,
      "dataPath": "~/naps2-client05"
    }
  ],
  "count": 5
}
```

---

### 3. ตรวจสอบสถานะ Clients ทั้งหมด

ดู health status ของทุก client

**Endpoint:** `GET /clients/status`

**ตัวอย่าง:**
```bash
curl http://localhost:9009/clients/status
```

**Response:**
```json
{
  "clients": [
    {
      "name": "client01",
      "port": 9061,
      "healthy": true,
      "status": "running"
    },
    {
      "name": "client02",
      "port": 9062,
      "healthy": true,
      "status": "running"
    },
    {
      "name": "client03",
      "port": 9063,
      "healthy": true,
      "status": "running"
    },
    {
      "name": "client04",
      "port": 9064,
      "healthy": true,
      "status": "running"
    },
    {
      "name": "client05",
      "port": 9065,
      "healthy": true,
      "status": "running"
    }
  ],
  "summary": {
    "total": 5,
    "healthy": 5,
    "unhealthy": 0
  }
}
```

**Status Values:**
- `running` - Client ทำงานปกติ
- `not_responding` - Client ไม่ตอบสนอง
- `unhealthy` - Client มีปัญหา
- `timeout` - Health check timeout

---

### 4. Restart Client

Restart client ที่ระบุ

**Endpoint:** `POST /clients/{clientName}/restart`

**Parameters:**
- `clientName` - ชื่อ client (client01, client02, client03, client04, client05)

**ตัวอย่าง:**

```bash
# Restart client01
curl -X POST http://localhost:9009/clients/client01/restart

# Restart client02
curl -X POST http://localhost:9009/clients/client02/restart

# Restart client03
curl -X POST http://localhost:9009/clients/client03/restart

# Restart client04
curl -X POST http://localhost:9009/clients/client04/restart

# Restart client05
curl -X POST http://localhost:9009/clients/client05/restart
```

**Response (สำเร็จ):**
```json
{
  "success": true,
  "message": "Client client02 restarted",
  "client": "client02",
  "port": 9062,
  "healthy": true,
  "status": "running"
}
```

**Response (ไม่พบ client):**
```json
{
  "error": "Client 'client99' not found"
}
```

**หมายเหตุ:**
- การ restart จะใช้เวลาประมาณ 5-7 วินาที
- Manager จะทำการ kill process เก่า รอ 2 วินาที start ใหม่ รอ 3 วินาที แล้วตรวจสอบ health

---

## สรุป API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | ตรวจสอบ manager server |
| GET | `/clients` | ดูรายการ clients ทั้งหมด |
| GET | `/clients/status` | ตรวจสอบ health status ทุก client |
| POST | `/clients/{name}/restart` | Restart client ที่ระบุ |

---

## Client Information

| Client | Port | Data Path |
|--------|------|-----------|
| client01 | 9061 | ~/naps2-client01 |
| client02 | 9062 | ~/naps2-client02 |
| client03 | 9063 | ~/naps2-client03 |
| client04 | 9064 | ~/naps2-client04 |
| client05 | 9065 | ~/naps2-client05 |

---

## ตัวอย่างการใช้งาน

### ตรวจสอบว่า clients ไหนมีปัญหา

```bash
curl http://localhost:9009/clients/status | grep -i unhealthy
```

### Restart ทุก client ทีละตัว

```bash
for i in {1..5}; do
  echo "Restarting client0$i..."
  curl -X POST http://localhost:9009/clients/client0$i/restart
  echo ""
done
```

### ดูสถานะแบบ real-time

```bash
watch -n 5 'curl -s http://localhost:9009/clients/status | python3 -m json.tool'
```

---

## Error Handling

### Error Codes

| Status Code | Meaning |
|-------------|---------|
| 200 | Success |
| 404 | Client not found หรือ endpoint ไม่มี |
| 500 | Internal server error |

### ตัวอย่าง Error Response

```json
{
  "error": "Client 'client99' not found"
}
```

---

## CORS Support

Manager Server รองรับ CORS ทุก endpoint สามารถเรียกจาก web browser ได้

**Headers:**
- `Access-Control-Allow-Origin: *`
- `Access-Control-Allow-Methods: GET, POST, OPTIONS`
- `Access-Control-Allow-Headers: Content-Type`

---

## Troubleshooting

### Port 9009 ถูกใช้งานอยู่

```bash
# หา process ที่ใช้ port
lsof -i :9009

# Kill process
kill -9 <PID>

# Start manager ใหม่
./run-manager.sh
```

### Client ไม่ตอบสนอง

```bash
# ตรวจสอบว่า client รันอยู่หรือไม่
curl http://localhost:9061/health

# Restart ผ่าน manager
curl -X POST http://localhost:9009/clients/client01/restart

# หรือ restart manual
ps aux | grep "NAPS2.*9061" | awk '{print $2}' | xargs kill -9
/path/to/NAPS2 --http-port 9061 --profile client01 --naps2-data ~/naps2-client01 &
```

---

## Notes

- Manager Server ต้องรันก่อนที่จะใช้งาน API
- แต่ละ client จะใช้ data directory แยกกัน
- การ restart จะไม่ทำให้ข้อมูลของ client สูญหาย
- Manager จะตรวจสอบ health โดยเรียก `/health` endpoint ของแต่ละ client
