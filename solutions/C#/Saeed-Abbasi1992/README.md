# Messaging Infra Challenge

This project is a sample messaging infrastructure using **RabbitMQ** and **C#**.  
The main goal is to send and receive **Info** and **Error** messages reliably, scalable, and fault-tolerant.

---

## Features

- **Info messages** sent via **Fanout Exchange** to multiple subscribers.
- **Error messages** sent via **Direct Exchange** to multiple ErrorWorkers.
- Supports **durable queues** and **persistent messages** to prevent message loss.
- Uses **Dead-Letter Exchange (DLX)** for rejected or expired messages.
- Automatic connection recovery (**Automatic Recovery**).
- Can run multiple **subscribers** or **workers** concurrently for scalability testing.

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) or later
- RabbitMQ: `docker run -it --rm --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management`
- Environment variables:

| Variable | Description | Example |
|----------|------------|--------|
| `AMQP_URI` | Full RabbitMQ connection URI | `amqp://guest:guest@localhost:5672/` |
| `RABBIT_HOST` | RabbitMQ host | `localhost` |
| `RABBIT_USER` | RabbitMQ username | `guest` |
| `RABBIT_PASS` | RabbitMQ password | `guest` |

## Setting Environment Variables

### Windows (PowerShell)
```powershell
$env:AMQP_URI="amqp://guest:guest@localhost:5672/"
$env:RABBIT_HOST="localhost"
$env:RABBIT_USER="guest"
$env:RABBIT_PASS="guest"
```

### Linux / Mac (bash/zsh)
```bash
export AMQP_URI="amqp://guest:guest@localhost:5672/"
export RABBIT_HOST="localhost"
export RABBIT_USER="guest"
export RABBIT_PASS="guest"
```

> Note: These variables are set for the current terminal session. To make them permanent, add them to `.bashrc` or `.zshrc`.

---

## Project Structure

```
solutions/
└─ C#/
   └─ Saeed-Abbasi1992/
      ├─ Producer/
      │  ├─ Program.cs
      │  ├─ InfoPublisher.cs
      │  └─ ErrorPublisher.cs
      │
      ├─ ErrorWorker/
      │  ├─ Program.cs
      │  └─ ErrorSubscriber.cs
      │
      ├─ InfoSubscriber/
      │  ├─ Program.cs
      │  └─ LogInfoSubscriber.cs
      │
      └─ SharedKernel/
         ├─ SharedKernel.csproj
         ├─ ConsoleLogger.cs
         ├─ Constants.cs
         ├─ AMQPUriHelper.cs
         ├─ RabbitConnectionHelper.cs
         │
         ├─ Interfaces/
         │  ├─ IPublisher.cs
         │  └─ ISubscriber.cs
         │
         └─ Models/
            ├─ MessageModel.cs
            ├─ InfoMessageModel.cs
            ├─ ErrorMessageModel.cs
            └─ SeverityType.cs

```

---

## Execution

1. **Build the project**
```bash
dotnet build
```

2. **Run Producer**
```bash
dotnet run --project solutions/C#/Saeed-Abbasi1992/Producer
```

3. **Run ErrorWorker** (at least two instances)
```bash
dotnet run --project solutions/C#/Saeed-Abbasi1992/ErrorWorker
```
```bash
dotnet run --no-build --project solutions/C#/Saeed-Abbasi1992/ErrorWorker
```

4. **Run InfoSubscriber** (one instance per service)
```bash
dotnet run --project solutions/C#/Saeed-Abbasi1992/InfoSubscriber -- grafana
```
```bash
dotnet run --no-build --project solutions/C#/Saeed-Abbasi1992/InfoSubscriber -- elk
```

> Note: Execution order does not matter; durable queues and persistent messages ensure that messages are not lost even if subscribers start later.

---

## Important Notes

- Each **InfoSubscriber** has its own dedicated queue.  
- **Fanout Exchange** ensures all active subscribers receive messages.  
- **ErrorWorker** consumes Error messages from the Direct Exchange.  
- **Dead-Letter Exchange (DLX)** stores messages that are Nack'ed or expired.  
- Channels and connections automatically recover if disconnected.

---

## Sample Message Models

### InfoMessageModel
- `Id` : Message ID
- `Service` : Service name
- `Message` : Message content
- `Latency` : Latency in milliseconds

### ErrorMessageModel
- `Id` : Message ID
- `Service` : Service name
- `Message` : Error message
- `SeverityType` : Severity (HIGH, MEDIUM, CRITICAL)