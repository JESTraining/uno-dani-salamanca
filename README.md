# Full-Stack Microservices Exercise: Real-time Order Processing System

## Project Overview
Build a microservices-based order processing system with real-time status updates using C#, React/Angular, Docker, and RabbitMQ, with **SQL databases** for all services.

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (React/Angular)                  │
│                   - Order Management Dashboard               │
│                   - Real-time Status Updates                 │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    API Gateway (C#)                         │
│                 - Authentication/Authorization              │
│                 - Request Routing                           │
│                 - Rate Limiting                             │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│ Order Service │   │Payment Service│   │Inventory Svc  │
│ (C#/SQL)      │   │ (C#/SQL)      │   │ (C#/SQL)      │
└───────────────┘   └───────────────┘   └───────────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              │
                        ┌─────────────┐
                        │  RabbitMQ   │
                        │  (Events)   │
                        └─────────────┘
```

**Database choices**: You can use SQL Server, PostgreSQL, or MySQL for each service (mix and match to learn different SQL dialects, or stick with one).

---

## Exercise Requirements

### Phase 1: Database Design & Order Service (Days 1-2)

#### Task 1.1: Design the Database Schema
**For each service, design and implement the database schema:**

**Order Service Database:**
- Design tables for orders and order items
- Consider: What fields does an order need? What statuses can it have?
- Implement proper indexes, constraints, and relationships
- Consider optimistic concurrency control (versioning)

**Payment Service Database:**
- Design tables for payments and transaction logs
- Consider: What payment methods? What statuses? How to handle failures?
- Implement audit trail for all payment attempts

**Inventory Service Database:**
- Design tables for products and inventory reservations
- Consider: How to handle stock levels? How to prevent overselling?
- Implement stock reservation with expiration

#### Task 1.2: Build Order Service
**Create a C# microservice with:**
- REST API endpoints:
  - `POST /api/orders` - Create new order (validate stock availability first via API call)
  - `GET /api/orders/{id}` - Get order details with items
  - `GET /api/orders` - List orders with pagination, filtering (by status, date range, customer)
  - `PUT /api/orders/{id}/status` - Update order status (with validation rules)
  - `DELETE /api/orders/{id}` - Cancel order (with business rules)
- Use Entity Framework Core or Dapper with SQL database
- Implement repository pattern and Unit of Work
- Implement idempotency for order creation (prevent duplicate orders)
- Publish events to RabbitMQ on order creation and status changes

**Business Rules to Enforce:**
- An order can only be cancelled if it's in 'Pending' or 'PaymentProcessing' status
- Orders with 'Shipped' or 'Delivered' status cannot be modified
- Total amount must be greater than 0
- At least one order item is required

---

### Phase 2: Payment & Inventory Services (Days 2-3)

#### Task 2.1: Payment Service
**Build a payment processing service:**
- REST API endpoints:
  - `POST /api/payments/process` - Process payment for an order
  - `GET /api/payments/{orderId}` - Get payment status for an order
  - `POST /api/payments/refund` - Process refund (bonus)
- Consume `OrderCreated` events from RabbitMQ
- Integrate with a **mock payment gateway** (simulate success/failure based on business logic)
- Implement transaction logging for all payment attempts
- Publish `PaymentProcessed` or `PaymentFailed` events
- Handle idempotency (prevent duplicate payment processing)

**Mock Payment Gateway Rules:**
- Payments > $10,000: Always fail (fraud detection)
- Payments ending with .99: 20% chance of failure (random)
- All other payments: Succeed
- Simulate 2-5 second processing delay (use async/await with Task.Delay)

#### Task 2.2: Inventory Service
**Create inventory management service:**
- REST API endpoints:
  - `GET /api/products` - List products with stock levels
  - `GET /api/products/{id}` - Get product details
  - `POST /api/products` - Add new product (admin only)
  - `PUT /api/products/{id}/stock` - Update stock levels
- Consume `PaymentProcessed` event to reserve inventory
- Reserve items and reduce stock
- Handle reservation timeout/release
- Publish `InventoryReserved` or `InventoryFailed` events
- Implement atomic stock operations using SQL transactions
- Prevent overselling with row-level locks or optimistic concurrency

**Inventory Logic:**
- Product stock should be reserved when payment is processed
- If inventory can't be reserved within 5 minutes, release and trigger failure
- Implement a scheduled job to clean up expired reservations (bonus)

---

### Phase 3: Message Queue Integration & Saga (Day 4)

#### Task 3.1: RabbitMQ Setup
**Implement event-driven communication:**
- Define event contracts (use JSON):
  - `OrderCreatedEvent` (OrderId, CustomerId, TotalAmount, Items, Timestamp)
  - `PaymentProcessedEvent` (OrderId, TransactionId, Amount, Timestamp)
  - `PaymentFailedEvent` (OrderId, Reason, Timestamp)
  - `InventoryReservedEvent` (OrderId, Items, Timestamp)
  - `InventoryFailedEvent` (OrderId, Reason, Timestamp)
  - `OrderCompletedEvent` (OrderId, Status, Timestamp)
- Set up RabbitMQ exchanges (topic or direct)
- Create queues for each service with appropriate bindings
- Implement dead letter queues for failed message processing
- Add retry mechanism with exponential backoff (3 retries, then DLQ)

#### Task 3.2: Implement Saga Pattern
**Create a distributed transaction coordinator:**

```
Order Saga Flow:
1. Order Created → Publish OrderCreatedEvent
2. Payment Service → Process Payment
   - Success: Publish PaymentProcessedEvent
   - Failure: Publish PaymentFailedEvent → Compensate (Cancel Order)
3. Inventory Service → Reserve Items
   - Success: Publish InventoryReservedEvent
   - Failure: Publish InventoryFailedEvent → Compensate (Refund Payment)
4. Order Service → Mark as Complete
   - Publish OrderCompletedEvent
```

**Implementation Options:**
- Choreography-based saga (events only)
- Orchestration-based saga (central coordinator)
- Or a hybrid approach

**Requirements:**
- All-or-nothing transaction guarantee
- Compensation logic for each step
- Idempotent event handlers
- Handle duplicate/missed events gracefully
- Log all saga steps for debugging

---

### Phase 4: Frontend Application (Day 5)

**Build a React or Angular application with:**

#### Pages/Views:
1. **Order Creation Form**
   - Product selection with real-time stock validation
   - Customer information
   - Form validation with error messages
   - Loading states during submission

2. **Order List Dashboard**
   - Table with all orders (paginated)
   - Filter by status, date range
   - Search by order number or customer
   - Real-time status updates via SignalR/WebSocket

3. **Order Details View**
   - Order information
   - Items list
   - Status timeline
   - Payment information
   - Action buttons (Cancel, if applicable)

4. **Admin Dashboard** (Bonus)
   - Total orders per day/week
   - Payment success/failure rate
   - Inventory alerts (low stock)
   - Revenue metrics

#### Technical Requirements:
- **State Management**: Redux/Context API (React) or NgRx (Angular)
- **UI Framework**: Material-UI, Ant Design, or Bootstrap
- **Real-time Updates**: SignalR client for .NET backend
- **API Client**: Axios or HttpClient with interceptors
- **Form Validation**: Reactive forms with custom validators
- **Error Handling**: Global error boundary, toast notifications
- **Responsive Design**: Mobile-first approach
- **Loading States**: Skeleton loaders or spinners

#### SignalR Integration:
- Connect to Order Service SignalR hub
- Subscribe to order status updates
- Update UI in real-time when status changes
- Implement reconnection logic

---

### Phase 5: Docker & DevOps (Day 6)

#### Task 5.1: Containerization
**Create Docker infrastructure:**
- Dockerfile for each microservice (multi-stage builds)
- Dockerfile for frontend (Nginx or Node)
- Docker Compose for local development
- Environment variables for all services
- Health checks for each container
- Volume mounting for development hot-reload

#### Docker Compose Requirements:
```yaml
Services:
  - sql-server (or postgres/mysql)
  - rabbitmq (with management plugin)
  - order-service
  - payment-service
  - inventory-service
  - api-gateway
  - frontend (nginx)
  - redis (for caching, optional)
  - seq (for logging, optional)
```

#### Task 5.2: Production Readiness
**Implement production features:**
- Serilog for structured logging with different sinks (Console, File, Seq)
- Health check endpoints (`/health`, `/ready`, `/live`)
- Distributed tracing with OpenTelemetry (Jaeger)
- Metrics collection (Prometheus)
- Configuration management (appsettings.json + environment variables)
- JWT Authentication with role-based authorization
- API versioning (v1, v2)
- OpenAPI/Swagger documentation
- CORS configuration

#### Task 5.3: CI/CD Pipeline (Bonus)
- GitHub Actions workflow
- Build and test on PR
- Docker image build and push to registry
- Deploy to staging environment

---

### Phase 6: Testing & Documentation (Day 7)

#### Task 6.1: Testing
**Write comprehensive tests:**
- **Unit Tests** (xUnit/NUnit):
  - Service layer logic
  - Domain models and business rules
  - Event handlers
  - Minimum 70% coverage

- **Integration Tests**:
  - Database operations (use TestContainers or in-memory)
  - RabbitMQ message publishing/consuming
  - API endpoints (WebApplicationFactory)

- **Frontend Tests** (Jest + React Testing Library):
  - Component rendering
  - User interactions
  - API integration with mocks

#### Task 6.2: Documentation
**Create comprehensive documentation:**
- **README.md**:
  - Project overview
  - Architecture diagram
  - Technology stack
  - Setup instructions (step by step)
  - Environment variables reference
  - How to run tests
  
- **API Documentation**:
  - Swagger/OpenAPI for all services
  - Postman collection
  - Sample requests/responses

- **Architecture Decision Record (ADR)**:
  - Why SQL? (Why not NoSQL?)
  - Why Saga pattern?
  - Database per service justification
  - Technology choices rationale

---

## Deliverables Summary

### Code:
- ✅ Complete solution with all 3 microservices
- ✅ API Gateway
- ✅ Frontend application
- ✅ Docker configuration
- ✅ RabbitMQ setup
- ✅ Unit and integration tests

### Documentation:
- ✅ README with setup instructions
- ✅ Swagger/OpenAPI documentation
- ✅ Architecture diagram
- ✅ ADR document

### DevOps:
- ✅ Docker Compose running all services
- ✅ Health checks implemented
- ✅ Logging configured
- ✅ (Bonus) CI/CD pipeline

---

## Evaluation Criteria

### Technical Skills (60%):
- **Architecture**: Proper microservices boundaries, database per service
- **C#/.NET**: Clean code, async/await, error handling, dependency injection
- **SQL**: Proper schema design, indexing, transaction handling, concurrency
- **RabbitMQ**: Proper exchange/queue setup, message reliability, retry logic
- **Frontend**: State management, real-time updates, clean component architecture
- **Docker**: Proper containerization, orchestration, environment management

### Problem-Solving (20%):
- Distributed transaction handling with Saga
- Idempotency implementation
- Concurrent stock management
- Error recovery and compensation

### Code Quality (20%):
- SOLID principles
- DRY code
- Proper naming conventions
- Code organization
- Test coverage
- Security practices (parameterization, validation, JWT)

---

## Bonus Challenges

Add these for extra credit:

1. **Circuit Breaker Pattern**: Implement Polly for resilience
2. **Caching**: Redis cache for product catalog
3. **Rate Limiting**: Add to API Gateway
4. **Monitoring**: Grafana + Prometheus dashboards
5. **CQRS Pattern**: Separate read and write models
6. **GraphQL**: Add GraphQL endpoint alongside REST
7. **Blue/Green Deployment**: Implement zero-downtime deployment
8. **Event Sourcing**: Store all events in event store
9. **gRPC**: Internal service communication with gRPC
10. **Feature Flags**: Implement feature toggle system

---

## Getting Started Tips

### Development Environment:
1. Install .NET 8 SDK
2. Install Node.js (18+) or Angular CLI
3. Install Docker Desktop
4. Install SQL Server/PostgreSQL/MySQL
5. Install RabbitMQ (or run via Docker)
6. IDE: Visual Studio 2022 or VS Code

### Suggested Folder Structure:
```
/
├── src/
│   ├── OrderService/
│   │   ├── OrderService.API/
│   │   ├── OrderService.Application/
│   │   ├── OrderService.Domain/
│   │   ├── OrderService.Infrastructure/
│   │   └── OrderService.Tests/
│   ├── PaymentService/
│   ├── InventoryService/
│   ├── ApiGateway/
│   └── Frontend/
├── docker/
│   ├── docker-compose.yml
│   ├── docker-compose.override.yml
│   └── .env
├── docs/
│   ├── architecture.md
│   ├── adr/
│   └── api/
├── scripts/
│   ├── setup-databases.sql
│   └── seed-data.sql
└── README.md
```

### Development Order:
1. Design the database schemas
2. Build Order Service with SQL
3. Build Payment Service (mock gateway)
4. Build Inventory Service (stock management)
5. Set up RabbitMQ and events
6. Implement Saga
7. Build Frontend
8. Dockerize everything
9. Add tests
10. Write documentation

---

## Time Management Guide

| Day | Focus Area | Key Deliverables |
|-----|-----------|------------------|
| Day 1 | Database Design + Order Service | Schemas, EF Core, CRUD APIs, RabbitMQ publisher |
| Day 2 | Payment Service | Payment processing, mock gateway, event consumers |
| Day 3 | Inventory Service | Stock management, reservations, event consumers |
| Day 4 | Integration + Saga | RabbitMQ setup, event handlers, saga implementation |
| Day 5 | Frontend | UI components, API integration, real-time updates |
| Day 6 | Docker + Production | Dockerfiles, compose, health checks, logging, auth |
| Day 7 | Testing + Documentation | Tests, README, Swagger, ADR |

---

**Good luck! This exercise will give you hands-on experience with all aspects of modern full-stack microservices development.**
