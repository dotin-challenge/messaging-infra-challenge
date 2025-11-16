# RabbitMQ Messaging Infrastructure Solution

**Author:** Arash Mousavi
**Language:** C# (.NET 8.0)
**Challenge:** Messaging Infrastructure for Sensitive Logs

## Architecture Overview

این solution دو الگوی messaging مختلف را پیاده‌سازی می‌کند:

### 1. Error Logs (Work Queue Pattern)
- **Exchange:** `logs.error.exchange` (Direct)
- **Queue:** `logs.error.q` (Durable)
- **Pattern:** Competing Consumers
- **Routing Key:** `error`
- **Behavior:** هر پیام فقط توسط یک worker پردازش می‌شود (fair dispatch)

### 2. Info Logs (Pub/Sub Pattern)
- **Exchange:** `logs.info.exchange` (Fanout)
- **Queues:** `logs.info.q.<service>` (یک queue به ازای هر subscriber)
- **Pattern:** Broadcast
- **Behavior:** همه subscribers تمام پیام‌ها را دریافت می‌کنند

## Components

### Producer
- تولید پیام‌های Error و Info
- پیام‌های persistent (survive broker restart)
- Environment variable support برای connection string
- Graceful shutdown

### ErrorWorker
- Competing consumer برای پیام‌های Error
- Manual ACK بعد از پردازش موفق
- Prefetch Count = 1 برای load balancing عادلانه
- Processing time بر اساس severity
- NACK و requeue در صورت خطا

### InfoSubscriber
- Fanout subscriber برای پیام‌های Info
- هر instance یک queue مجزا
- Service name از command line argument
- Real-time broadcast

## Prerequisites

### 1. RabbitMQ Server

باید RabbitMQ روی سیستم یا cloud در دسترس باشد.

**نصب local (Docker):**
```bash
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management
```

**نصب local (Ubuntu/Debian):**
```bash
sudo apt-get update
sudo apt-get install rabbitmq-server
sudo systemctl enable rabbitmq-server
sudo systemctl start rabbitmq-server
```

**Management UI:** http://localhost:15672 (guest/guest)

### 2. .NET SDK

.NET 8.0 یا بالاتر:
```bash
dotnet --version
```

## Configuration

### Environment Variables

یکی از دو روش زیر:

**Option 1: AMQP_URI (Recommended)**
```bash
export AMQP_URI="amqp://user:pass@localhost:5672/"
```

**Option 2: Individual Variables**
```bash
export RABBIT_HOST="localhost"
export RABBIT_USER="guest"
export RABBIT_PASS="guest"
```

**Default:** اگر environment variable تنظیم نشود، از `amqp://guest:guest@localhost:5672/` استفاده می‌شود.

### Optional Variables

```bash
export WORKER_ID="worker-01"
```

## Build

```bash
cd solutions/C#/arash.mousavi

dotnet build Producer/Producer.csproj
dotnet build ErrorWorker/ErrorWorker.csproj
dotnet build InfoSubscriber/InfoSubscriber.csproj
```

## Run

### Terminal 1: Producer
```bash
cd solutions/C#/arash.mousavi/Producer
dotnet run
```

**Output:**
```
Producer started. Press Ctrl+C to exit.

[Producer] Sent Info  id=I-5541 service=web msg="GET /api/web 200" latency_ms=42
[Producer] Sent Info  id=I-7823 service=auth msg="GET /api/auth 200" latency_ms=156
[Producer] Sent Error id=E-1023 service=auth msg="DB timeout" severity=HIGH
```

### Terminal 2: ErrorWorker #1
```bash
cd solutions/C#/arash.mousavi/ErrorWorker
dotnet run
```

### Terminal 3: ErrorWorker #2
```bash
cd solutions/C#/arash.mousavi/ErrorWorker
WORKER_ID=worker-02 dotnet run
```

**Output:**
```
[ErrorWorker-abc123] Waiting for error messages.

[ErrorWorker-abc123] E-1023 received (severity=HIGH)
[ErrorWorker-abc123] E-1023 processed successfully → acked
```

### Terminal 4: InfoSubscriber (Grafana)
```bash
cd solutions/C#/arash.mousavi/InfoSubscriber
dotnet run -- grafana
```

### Terminal 5: InfoSubscriber (ELK)
```bash
cd solutions/C#/arash.mousavi/InfoSubscriber
dotnet run -- elk
```

**Output:**
```
[InfoSub-grafana] Subscribed to info logs.

[InfoSub-grafana] I-5541 -> GET /api/web 200 (latency: 42ms)
[InfoSub-grafana] I-7823 -> GET /api/auth 200 (latency: 156ms)
```

## Testing

### Test Work Queue (Load Balancing)

1. Start Producer
2. Start 2-3 ErrorWorkers
3. مشاهده کنید که Error messages به صورت round-robin بین workers توزیع می‌شوند
4. یک worker را kill کنید → پیام‌ها به worker های دیگر می‌روند

### Test Pub/Sub (Broadcasting)

1. Start Producer
2. Start 2-3 InfoSubscribers با نام‌های مختلف (grafana, elk, splunk)
3. مشاهده کنید که همه subscribers هر Info message را دریافت می‌کنند
4. یک subscriber را kill کنید → بقیه همچنان پیام می‌گیرند

### Test Persistence

1. Stop Producer and all consumers
2. Restart RabbitMQ: `sudo systemctl restart rabbitmq-server`
3. Start consumers → پیام‌های queue شده پردازش می‌شوند (چون durable=true)

## Key Features

- **Durable Queues & Persistent Messages:** survive broker restart
- **Manual ACK:** اطمینان از پردازش موفق قبل از حذف پیام
- **Prefetch QoS:** fair dispatch برای load balancing
- **Graceful Shutdown:** Ctrl+C handling
- **Error Handling:** NACK و requeue در صورت خطا
- **Environment Variables:** no hardcoded credentials
- **Scalability:** horizontal scaling بدون downtime
- **Async/Await:** modern C# patterns
- **Structured Logging:** JSON payloads با timestamp

## Architecture Decisions

### چرا Direct Exchange برای Errors؟
- امکان routing به queues مختلف در آینده (error.critical, error.warning)
- الگوی work queue برای competing consumers

### چرا Fanout Exchange برای Info؟
- broadcast به همه subscribers
- هر service queue مجزا → independence

### چرا Prefetch=1؟
- fair dispatch: worker سریع‌تر، پیام بیشتر می‌گیرد
- load balancing بهتر بین workers

### چرا Manual ACK؟
- reliability: اگر worker crash کند، پیام به queue برمی‌گردد
- at-least-once delivery guarantee

## Message Schema

### Error Log
```json
{
  "id": "E-1023",
  "service": "auth",
  "msg": "DB timeout",
  "severity": "HIGH",
  "timestamp": "2025-11-16T20:00:00.000Z"
}
```

### Info Log
```json
{
  "id": "I-5541",
  "service": "web",
  "msg": "GET /api/orders 200",
  "latency_ms": 42,
  "timestamp": "2025-11-16T20:00:00.000Z"
}
```

## Troubleshooting

### Connection Refused
```bash
sudo systemctl status rabbitmq-server
sudo systemctl start rabbitmq-server
```

### Check Queues
```bash
sudo rabbitmqctl list_queues name messages consumers
```

### Check Exchanges
```bash
sudo rabbitmqctl list_exchanges name type
```

### Management UI
http://localhost:15672

## License

MIT
