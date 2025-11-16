Messaging Infrastructure Challenge – RabbitMQ & .NET

This project implements a messaging backbone for a live, critical system that handles logs with two distinct flows:
1- Error Logs: Critical messages that must be processed by only one worker at a time, with balanced load distribution (Work Queue pattern).
2- Info Logs: General monitoring messages sent to all subscribers simultaneously (Fanout / Pub-Sub pattern).
The system is designed for high availability, horizontal scalability, and fault tolerance.
------------------------------------------------------------------------------------------------------------------------------------------

Prerequisites:
.NET 9 SDK
Docker (for RabbitMQ and Redis)
RabbitMQ (local or via Docker)
Redis (for Idempotency of Error logs)
------------------------------------------------------------------------------------------------------------------------------------------

Docker Setup:

Run RabbitMQ and Redis with Docker Compose:

docker-compose up -d


RabbitMQ: amqp://guest:guest@localhost:5672
Redis: localhost:6379
------------------------------------------------------------------------------------------------------------------------------------------

Environment Variables:

Set the following environment variables:

export AMQP_URI=amqp://guest:guest@localhost:5672/
export PREFETCH_COUNT=2
export REDIS_URI=localhost:6379


Windows PowerShell:

$env:AMQP_URI="amqp://guest:guest@localhost:5672/"
$env:PREFETCH_COUNT="2"
$env:REDIS_URI="localhost:6379"
------------------------------------------------------------------------------------------------------------------------------------------

Project Structure
solutions/C#/KevinMKM/
├─ Producer/        # Sends Error and Info messages
├─ ErrorWorker/     # Processes Error messages
├─ InfoSubscriber/  # Receives Info messages
├─ Shared/          # Shared classes and helpers
├─ docker-compose.yml
├─ Dockerfile.Producer
├─ Dockerfile.ErrorWorker
├─ Dockerfile.InfoSubscriber
└─ ReadMe.md
------------------------------------------------------------------------------------------------------------------------------------------

Running the Services:

1. Producer:

dotnet run --project Producer

Produces Error and Info messages.
Error: Confirm + Retry.
Info: Fire-and-forget.


2. ErrorWorker (minimum 2 instances):

dotnet run --project ErrorWorker
dotnet run --project ErrorWorker

Each Error message processed by only one worker.
Redis ensures Idempotency across restarts.
Failed messages are sent to DLX.


3. InfoSubscriber (minimum 2 instances):

dotnet run --project InfoSubscriber -- grafana
dotnet run --project InfoSubscriber -- elk

Each subscriber has its own dedicated queue.
Messages are broadcast to all subscribers (Fanout).
Handles malformed messages gracefully with proper acks.
------------------------------------------------------------------------------------------------------------------------------------------

Features:

Error Flow:
	Work Queue pattern with multiple workers.
	Redis-based Idempotency to avoid duplicates.
	DLX for failed messages.
	Prefetch and QoS configured.
Info Flow:
	Fanout exchange for real-time broadcasting.
	Dedicated queue per subscriber.
	Automatic reconnect and resubscribe.
	Ack for malformed messages.
Producer:
	Confirm + Retry for Error messages.
	Fire-and-forget for Info messages.
Docker Integration:
	docker-compose.yml for RabbitMQ, Redis, and all services.
	Dockerfiles for Producer, ErrorWorker, InfoSubscriber.
	Single command to build and run all services.
------------------------------------------------------------------------------------------------------------------------------------------

Quick Start with Docker:

docker-compose build
docker-compose up


Producer starts sending messages.
ErrorWorkers process Error messages.
InfoSubscribers receive Info messages.
Check console logs to verify flow.
------------------------------------------------------------------------------------------------------------------------------------------

Notes:

Redis is required for Idempotency in the Error flow.
PREFETCH_COUNT and other ENV variables can be tuned for load testing.
DLX ensures failed Error messages are not lost.