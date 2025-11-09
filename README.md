# Messaging Infrastructure for Sensitive Logs (RabbitMQ)

[![Difficulty](https://img.shields.io/badge/difficulty-medium-brightgreen)]()
[![Language](https://img.shields.io/badge/language-C%23-informational)]()
[![Deadline](https://img.shields.io/badge/deadline-2025--11--09-critical)]()

> Design and implement a message-driven logging backbone using RabbitMQ that supports **two concurrent patterns**: (1) **Work Queue Distribution** for `Error` logs (each message handled by exactly one consumer, fairly balanced), and (2) **Fanout Broadcasting** for `Info` logs (real-time delivery to all active subscribers).

---

## Table of Contents

* [Requirements](#requirements)
* [Problem Description](#problem-description)
* [Rules & Constraints](#rules--constraints)
* [Architecture Guide](#architecture-guide)
* [Input/Output Examples](#inputoutput-examples)
* [How to Run & Test](#how-to-run--test)
* [How to Submit (PR)](#how-to-submit-pr)
* [Evaluation Criteria](#evaluation-criteria)
* [Timeline](#timeline)
* [Contact](#contact)

---

## Requirements

* Language: **C# (.NET 6+)**
* OS: Windows / Linux / macOS
* Participants are responsible for installing and running **RabbitMQ** locally or in the cloud. **No template or Docker Compose file** is provided in this repository.
* Use the official C# client library: `RabbitMQ.Client`.

---

## Problem Description

You are designing a **resilient and scalable messaging backbone** for a live, mission-critical system.

* **Critical logs (`Error`)**: Each message must be consumed by **exactly one** worker. Load must be **balanced** between active workers (Work Queue / Competing Consumers pattern).
* **General logs (`Info`)**: Each message must be delivered to **all** connected monitoring services **in real time** (Fanout / Pub-Sub pattern).

System requirements:

* Must tolerate temporary network issues (using retries, backoff, or publisher confirms).
* Must scale horizontally without downtime.
* Must support both log flows efficiently using one shared RabbitMQ cluster if desired.

**Deliverables:**

1. **Producer** that generates `Error` and `Info` messages with structured payloads.
2. **Error Workers** (≥2 instances) processing messages with fair dispatch and manual ACK.
3. **Info Subscribers** (≥2 independent services), each with its own queue.
4. Structured and readable console logging.
5. A short `README` inside your solution folder describing how to run your version.

---

## Rules & Constraints

* Use RabbitMQ primitives appropriately:

  * `Error` flow: classic **work queue** (direct or default exchange), **durable** queue, `prefetch`, **manual ACK**.
  * `Info` flow: **fanout** exchange, one **durable** queue per subscriber.
* **No prebuilt templates or Docker Compose** files are included. You must configure RabbitMQ and scripts yourself.
* Use environment variables for sensitive data (no hardcoded credentials).
* Ensure reliability and handle reconnects/retries gracefully.
* Keep code clean, modular, and readable.

---

## Architecture Guide

* Suggested Exchanges:

  * `logs.error.exchange` (direct) → queue: `logs.error.q` (competing consumers)
  * `logs.info.exchange` (fanout) → queues: `logs.info.q.<service>`
* Set **`basic.qos(prefetch)`** to limit unacknowledged messages per worker.
* Consider **publisher confirms** or idempotent retries for at-least-once delivery on the `Error` stream.

---

## Input/Output Examples

**Producer**

```
[Producer] Sent Error id=E-1023 service=auth msg="DB timeout" severity=HIGH
[Producer] Sent Info  id=I-5541 service=web  msg="GET /api/orders 200" latency_ms=42
```

**Error Worker (A)**

```
[ErrorWorker-A] E-1023 received … processing … acked
```

**Info Subscriber (grafana)**

```
[InfoSub-grafana] I-5541 -> dashboard updated
```

---

## How to Run & Test

### 1) Clone the repository

```bash
git clone https://github.com/dotin-challenge/messaging-infra-challenge.git
cd messaging-infra-challenge
```

### 2) Environment setup

* RabbitMQ must be available (e.g., `amqp://user:pass@host:5672/`).
* Set environment variables:

  * `AMQP_URI`
  * or separately: `RABBIT_HOST`, `RABBIT_USER`, `RABBIT_PASS`

### 3) Folder structure (required)

```
solutions/<language>/<username>/
  ├─ Producer/
  ├─ ErrorWorker/
  └─ InfoSubscriber/
```

> Example for C#: `solutions/C#/YOUR_NAME/Producer`

### 4) Build & Run (C#)

```bash
dotnet build
# Terminal 1: Producer
dotnet run --project solutions/C#/YOUR_NAME/Producer
# Terminals 2 & 3: Error Workers (at least two)
dotnet run --project solutions/C#/YOUR_NAME/ErrorWorker
dotnet run --project solutions/C#/YOUR_NAME/ErrorWorker
# Terminals 4 & 5: Info Subscribers (pass service name)
dotnet run --project solutions/C#/YOUR_NAME/InfoSubscriber -- grafana
dotnet run --project solutions/C#/YOUR_NAME/InfoSubscriber -- elk
```

### 5) Tests (optional)

```bash
dotnet test
```

---

## How to Submit (PR)

1. **Fork** the repository.
2. Create a new branch:

   ```bash
   git checkout -b solution/<username>
   ```
3. Place your code under:

   ```
   solutions/<language>/<username>/
     ├─ source files
     └─ README.md
   ```
4. Open a Pull Request with the title:

   ```
   [Solution] Messaging Infrastructure for Sensitive Logs (RabbitMQ) - <username>
   ```

---

## Evaluation Criteria

| Criterion                        | Weight |
| -------------------------------- | -----: |
| Correctness                      |    40% |
| Code Quality & Readability       |    25% |
| Error Handling & Resilience      |    10% |
| Output Formatting & Developer DX |    10% |
| Documentation                    |     5% |
| **Submission Speed (PR Time)**   | **5%** |

> The earlier you submit a correct and working PR before the deadline, the higher your chance to earn these extra 5%.

---

## Timeline

* **Start:** 2025-11-09
* **PR Submission Deadline:** 2025-11-16

---

## Contact

* Repository: [https://github.com/dotin-challenge/messaging-infra-challenge](https://github.com/dotin-challenge/messaging-infra-challenge)
* GitHub Issues
* .NET Community Group
