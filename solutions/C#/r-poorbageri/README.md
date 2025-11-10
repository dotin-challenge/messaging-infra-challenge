# # Messaging Infrastructure for Sensitive Logs (RabbitMQ)

A message-driven architecture built on RabbitMQ with three services: **Producer** (Web API for publishing logs), **ErrorWorker** (consumer for error logs with manual acknowledgements), and **InfoSubscriber** (consumer for info logs via fanout).  
The system is designed to **scale horizontally without downtime**, ensuring messages are not lost during scaling or restarts.

---

## Architecture

- **Core components:** RabbitMQ, Producer, ErrorWorker, InfoSubscriber
- **Messaging patterns:**
  - **Error logs:** Direct exchange → durable queue → manual ack
  - **Info logs:** Fanout exchange → each subscriber has its own durable queue → manual ack
- **Horizontal scaling:** Multiple instances of workers/subscribers run concurrently; RabbitMQ distributes messages across instances.
- **Resilience:** Durable exchanges/queues, manual ack/nack, automatic connection recovery, and health checks.

---

## Folder Structure


```md
project-root/
├── docker-compose.yml
├── README.md
├── Producer/
│ ├── Producer.csproj
│ ├── Program.cs
│ ├── Dockerfile
│ └── LoggingLib/
│   ├── IRabbitLogger.cs
│   ├── RabbitLogger.cs
│   └── RabbitLogLevel.cs
├── ErrorWorker/
│ ├── ErrorWorker.csproj
│ ├── Program.cs
│ └── Dockerfile
└── InfoSubscriber/
│ ├── InfoSubscriber.csproj
│ ├── Program.cs
│ └── Dockerfile


```
---

## Prerequisites

- **Docker and Docker Compose** installed and running.
- **Open ports:**
  - RabbitMQ: `5672` (AMQP), `15672` (Management UI)
  - Producer: `5000` mapped to container `8080`
- **Environment variables:**
  - `RABBITMQ_HOST`: RabbitMQ hostname inside the Compose network (default: `rabbitmq`)
  - `RABBITMQ_USER` / `RABBITMQ_PASS`: Credentials (default: `user` / `pass`)
  - `SERVICE_NAME`: Optional display name for logs per instance

---

## Quick Start

1. **Build and start all services:**
   ```bash
   docker-compose up --build -d

1. **Scale horizontally:**
	```bash
	**# At least two ErrorWorkers**
	docker-compose up --scale errorworker=2 -d

	**# Two InfoSubscribers**
	docker-compose up --scale infosubscriber=2 -d
   
  3. **Optionally run named instances in separate terminals:**
	  ```bash
	  docker-compose run --rm -e SERVICE_NAME=ErrorWorker1 errorworker
	 docker-compose run --rm -e SERVICE_NAME=ErrorWorker2 errorworker
	 docker-compose run --rm -e SERVICE_NAME=InfoSubscriber1 infosubscriber
	 docker-compose run --rm -e SERVICE_NAME=InfoSubscriber2 infosubscriber
## Usage

-   **RabbitMQ UI:**
    
    -   URL:http://localhost:15672
    -   Credentials: `user`  / `pass`
        
-   **Send logs via Producer:**
    
    -   Info log:
        
        ```bash
	        curl -X POST "http://localhost:5000/log/info?message=HelloWorld"
	- Error log:
	
		```bash
		curl -X POST "http://localhost:5000/log/error?message=SomethingFailed"

    - Inspect containers and logs:
	    ```bash
	    docker ps
	    docker logs <container-name>
	 ## Design Details and Best Practices

-   **Durability:**
    
    -   Exchanges and queues are declared durable so messages persist across restarts.
        
    -   Error queue is bound via a direct exchange with routing key `error`.
        
-   **Manual acknowledgements:**
    
    -   Workers consume with `autoAck=false`; on success they call `BasicAck`, on failure `BasicNack`  with `requeue=true`.
        
    -   Ensures messages are only removed after successful processing.
        
-   **Automatic recovery:**
    
    -   RabbitMQ connections use automatic recovery, heartbeat, and reconnection intervals for resilience.
        
-   **Scalability:**
    
    -   **ErrorWorkers:**  Multiple instances share the same queue; RabbitMQ load-balances deliveries.
        
    -   **InfoSubscribers:**  Fanout exchange; each instance can have its own durable queue to receive all broadcasted messages.
        
-   **Observability:**
    
    -   Clear, color-coded console output; errors also written to stderr.
        
    -   `SERVICE_NAME`  helps distinguish which instance processed a message.
        

## Troubleshooting

-   **Containers not starting:**
    
    -   Check `docker-compose ps`  and RabbitMQ health status.
        
    -   Ensure ports are free and Docker is running.
        
-   **Producer cannot reach RabbitMQ:**
    
    -   `RABBITMQ_HOST`  must be `rabbitmq`  within the Compose network.
        
    -   Do not use `localhost`  inside containers.
        
-   **Messages not consumed:**
    
    -   Workers must start with `autoAck=false`  and perform `BasicAck`.
        
    -   Review container logs for connection or processing errors.
        
-   **Scaling shows only one instance:**
    
    -   Start services detached with `docker-compose up -d`, then apply `--scale`.
        

## Notes

-   **Compose version:**  The project uses Compose file format `3.9`, compatible with modern Docker Engine versions.
    
-   **Production hint:**  For zero-downtime rolling updates and autoscaling, consider Kubernetes or Docker Swarm; the current design already supports horizontal scaling without downtime at the messaging layer.
