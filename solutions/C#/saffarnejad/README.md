# Messaging Infrastructure for Sensitive Logs

A C# implementation of a message-driven logging backbone using RabbitMQ that supports both Work Queue distribution for Error logs and Fanout broadcasting for Info logs.

## Architecture

- **Error Logs**: Use Work Queue pattern with competing consumers, manual ACK, and fair dispatch
- **Info Logs**: Use Fanout pattern with dedicated queues per subscriber service
- **Resilience**: Automatic reconnection, retry logic, and durable queues

## Prerequisites

- .NET 6.0 SDK
- RabbitMQ server (local or cloud)
- Environment variables for connection (optional)

## Setup

1. **RabbitMQ Setup** (Docker example):
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

## Running the Solution

1. **Build the solution:**
```bash
dotnet build
```

2. **Start the Producer** (Terminal 1):
```bash
dotnet run --project Producer
```

3. **Start Error Workers** (Terminals 2 & 3):
```bash
dotnet run --project ErrorWorker
dotnet run --project ErrorWorker
```

4. **Start Info Subscribers** (Terminals 4 & 5):
```bash
dotnet run --project InfoSubscriber -- grafana
dotnet run --project InfoSubscriber -- elk
```

## Features

- **Error Flow:**
  - Direct exchange with single durable queue
  - Manual acknowledgment with fair dispatch (prefetch=1)
  - Processing time varies by error severity
  - Automatic retry on failure

- **Info Flow:**
  - Fanout exchange with separate queues per subscriber
  - Automatic acknowledgment
  - Real-time delivery to all active subscribers

- **Resilience:**
  - Automatic reconnection with exponential backoff
  - Durable messages and queues
  - Error handling and logging

## Output Examples

```text
[Producer] Sent Error id=E-1023 service=auth msg="DB timeout" severity=HIGH
[ErrorWorker-A] E-1023 received from service=auth - processing...
[ErrorWorker-A] E-1023 processed successfully - acked

[Producer] Sent Info  id=I-5541 service=web msg="GET /api/orders 200" latency_ms=42
[InfoSub-grafana] I-5541 -> dashboard updated (latency: 42ms)
[InfoSub-elk] I-5541 -> indexed in elasticsearch (latency: 42ms)
```

