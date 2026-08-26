# Transaction Processor

Event-driven background service that ingests mock financial transaction data from three sources, categorises each transaction, and persists raw transactions and pre-computed aggregations to MySQL. Built in **C# / .NET 8**.

## What It Does

1. **MockDataIngestionHostedService** — fires every 5 seconds, generates batches of mock transactions from three sources (BankAccount, CreditCard, Investment) and publishes `TransactionEvent` messages to the `transaction-events` Kafka topic.
2. **TransactionProcessingHostedService** — polls Kafka every 10 seconds, deserialises events, delegates a batch to `TransactionProcessingService`.
3. **TransactionProcessingService** (the heavy lifter) — for each event: categorises via keyword rules, persists to `transactions` table, upserts `transaction_aggregations` (ALL_TIME + MONTHLY), with Polly exponential-backoff retry on transient MySQL errors.

## Transaction Categories

Groceries, Fuel, Utilities, Entertainment, Dining, Healthcare, Travel, Shopping, Salary, Transfer, ATM, Insurance, Education, Subscriptions, Other

## Tech Stack

| Concern | Choice |
|---------|--------|
| Language | C# 12 / .NET 8 |
| Messaging | Confluent.Kafka 2.x (Redpanda broker) |
| ORM | Dapper + MySQL stored procedures |
| Resilience | Polly 8.x (exponential backoff + jitter) |
| Logging | Serilog → Console (structured JSON) |
| Tests | xUnit + NSubstitute + FluentAssertions |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Running the Full Stack (Docker Compose)

Run from the `transaction-aggregation-api` directory (Docker Compose lives there):

```bash
cd ../transaction-aggregation-api
docker compose up --build
```

---

## Running Locally

```bash
# 1. Apply database schema (MySQL must be running)
mysql -u root -p < database/01_schema.sql
mysql -u root -p transaction_db < database/02_stored_procedures.sql

# 2. Start the processor
cd src/TransactionProcessor
dotnet run
```

Health endpoints:
- http://localhost:8080/health/liveness
- http://localhost:8080/health/readiness

---

## Running Tests

```bash
dotnet test tests/TransactionProcessor.Tests/
```

---

## Database

SQL files are in `database/`:
- `01_schema.sql` — creates `transaction_db`, tables, indexes, and the `txuser` MySQL user
- `02_stored_procedures.sql` — all stored procedures (write path: `usp_InsertTransaction`, `usp_UpsertAggregation`)

---

## Project Structure

```
src/TransactionProcessor/
├── Application/
│   ├── Domain/
│   │   ├── Constants/          # App-wide constants, mock merchant lists
│   │   ├── Entities/           # DB models
│   │   ├── Enums/
│   │   └── Events/             # TransactionEvent (Kafka message shape)
│   ├── Repositories/           # Dapper + stored procedure calls
│   └── Services/
│       ├── Contracts/          # Interfaces
│       ├── Hosted/             # Thin IHostedService shells
│       ├── CategoryService.cs  # Keyword rules engine
│       ├── KafkaConsumerService.cs
│       ├── KafkaProducerService.cs
│       └── TransactionProcessingService.cs  # Heavy lifter
└── Core/Extensions/            # DI registrations
database/
tests/TransactionProcessor.Tests/
```

---

## GitHub

- **Transaction Processor**: https://github.com/ElonaFrance/transaction-processor
- **Transaction Aggregation API**: https://github.com/ElonaFrance/transaction-aggregation-api

**Language:** C# / .NET 8 | **Area:** Backend
