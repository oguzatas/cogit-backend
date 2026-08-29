# Cogit Backend

> **Project status:** archived and open sourced for reference.

client [source code](https://github.com/oguzatas/cogit-admin)

Cogit  is a multi-tenant assessment platform API built with ASP.NET Core and Clean Architecture.  
It manages tenants, departments, employees, tests, questions, assignments, scoring, and reporting.

## What it does

- Provides role-based APIs for **SuperAdmin** and **TenantStaff**
- Supports assignment lifecycle: create assignment, answer questions, submit, and evaluate
- Includes a guest access flow for test takers via magic-link style access keys
- Calculates results with formula-based scoring (NCalc) and supports manual grading
- Exposes audit logs and dashboard metrics for operational visibility

## Tech stack

- **.NET 10** (SDK `10.0.102`)
- **ASP.NET Core Minimal APIs**
- **Entity Framework Core** + **PostgreSQL** (Npgsql)
- **ASP.NET Core Identity** + **JWT auth** (access + refresh token flow)
- **MediatR** + **FluentValidation**
- **.NET Aspire** (AppHost + service defaults + dashboard)
- **OpenAPI + Scalar** for API exploration

## Solution structure

- `/src/Domain` — core entities, enums, domain rules
- `/src/Application` — use-cases (commands/queries), validation, business logic
- `/src/Infrastructure` — persistence, identity, database setup/seeding
- `/src/Web` — HTTP API endpoints and host configuration
- `/src/AppHost` — Aspire orchestration entry point
- `/tests` — unit, integration, and functional test projects

## Getting started

### Prerequisites

- .NET SDK 10.0.102+
- Docker (for local PostgreSQL)

### 1) Start PostgreSQL

```bash
docker compose up -d
```

### 2) Run the application

Recommended (with Aspire dashboard):

```bash
dotnet run --project ./src/AppHost
```

Alternative (API only):

```bash
dotnet run --project ./src/Web
```

### 3) Open API docs

- Scalar UI: `http://localhost:<port>/scalar`
- OpenAPI document: `http://localhost:<port>/openapi/v1.json`

## Build and test

```bash
dotnet build
dotnet test
```

## Configuration notes

- Database connection and JWT settings are read from `src/Web/appsettings.json`.
- CORS allowed origins are configured under `AllowedOrigins`.
- In development, database migrations and seed steps run automatically at startup.

## Security notes for open-source usage

- Replace default local/dev secrets (database password, JWT secret, seeded credentials) before any real deployment.
- Prefer environment variables or Azure Key Vault for production secrets.
