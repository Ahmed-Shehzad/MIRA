# Low-Level System Architecture – UML Diagrams

This document contains process diagrams, flowcharts, sequence diagrams, and domain model diagrams for the HIVE Food Ordering system. Each diagram is followed by a written explanation.

---

## 1. Domain Model (Class Diagram)

```mermaid
classDiagram
    class AppUser {
        +string Id
        +string CognitoUsername
        +string Email
        +string Company
        +IList~string~ Groups
    }

    class OrderRound {
        +int Id
        +string RestaurantName
        +string? RestaurantUrl
        +string CreatedByUserId
        +DateTime Deadline
        +OrderRoundStatus Status
        +Create()
        +Update()
        +Close()
    }

    class OrderItem {
        +int Id
        +int OrderRoundId
        +string UserId
        +string Description
        +decimal Price
        +string? Notes
        +Add()
        +Update()
        +Remove()
    }

    class OrderRoundStatus {
        <<enumeration>>
        Open
        Closed
    }

    AppUser "1" --> "*" OrderRound : creates
    AppUser "1" --> "*" OrderItem : owns
    OrderRound "1" --> "*" OrderItem : contains
    OrderRound --> OrderRoundStatus : has
```

### Event-Driven Flow

```mermaid
sequenceDiagram
    participant API as API Controller
    participant Handler as OrderRoundHandler
    participant DB as DbContext
    participant MT as MassTransit
    participant MQ as SNS/SQS
    participant C as Consumer

    API->>Handler: CreateAsync(request)
    Handler->>DB: Add round, SaveChanges
    Handler->>MT: Publish(OrderRoundCreatedEvent)
    MT->>MQ: Publish to topic / queue
    Handler-->>API: OrderRoundResponse
    MQ->>C: OrderRoundCreatedEvent
    C->>C: Log / Process
```

**Explanation:** After a handler persists state, it publishes domain events via MassTransit. **Production**: events go to AWS SNS (topics), which fan out to SQS queues; consumers process from SQS. **Development**: LocalStack (SQS/SNS emulation). **Tests**: InMemory. All transports use open-source libraries (MassTransit, AWS SDK: Apache 2.0).

---

**Explanation:** This class diagram shows the core domain entities and their relationships. **AppUser** is provisioned from AWS Cognito (Id = Cognito sub); it has `Company` and `Groups` (cached from cognito:groups). Each user can create many **OrderRounds** (one-to-many) and own many **OrderItems** (one-to-many). An **OrderRound** has a restaurant name, URL, deadline, and status (Open or Closed); it contains many **OrderItems**. Each **OrderItem** belongs to one round and one user, and has description, price, and optional notes. The arrows indicate cardinality: `1` → `*` means one entity relates to many.

---

## 2. Entity Relationship Diagram

```mermaid
erDiagram
    Users {
        string Id PK
        string CognitoUsername
        string Email
        string Company
        int TenantId FK
        string Groups
    }

    Tenants {
        int Id PK
        string Name
        string Slug
        bool IsActive
    }

    OrderRounds {
        int Id PK
        string RestaurantName
        string RestaurantUrl
        string CreatedByUserId FK
        datetime Deadline
        string Status
    }

    OrderItems {
        int Id PK
        int OrderRoundId FK
        string UserId FK
        string Description
        decimal Price
        string Notes
    }

    Users }o--|| Tenants : "belongs to"
    Users ||--o{ OrderRounds : "created by"
    Users ||--o{ OrderItems : "owns"
    OrderRounds ||--o{ OrderItems : contains
```

**Explanation:** This ER diagram shows the database schema. **Users** stores Cognito-provisioned users (Id = Cognito sub); **Groups** is a comma-separated list of Cognito group names (e.g. Admins, Managers, Users). **OrderRounds** has a foreign key to the user who created it. **OrderItems** references both the order round and the user who added it. PK = primary key, FK = foreign key. The `||--o{` notation means "one to many" (one user creates many rounds; one round contains many items).

---

## 3. Sequence Diagrams

Sequence diagrams show the order of messages between components over time. Read from top to bottom; arrows indicate requests (solid) and responses (dashed). `alt` blocks show alternative paths.

### 3.1 Cognito Login Flow

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant C as Cognito Hosted UI
    participant A as AuthController
    participant M as CognitoUserProvisioningMiddleware
    participant DB as DbContext

    U->>F: Click "Sign in with Cognito"
    F->>C: Redirect to Cognito Hosted UI
    U->>C: Authenticate (email/password or SSO)
    C->>F: Redirect /login#id_token=JWT
    F->>F: Store token, call GET /auth/me
    F->>A: GET /auth/me (Bearer token)
    A->>M: JWT validated, provision AppUser
    M->>DB: ProvisionOrFindAsync (from cognito:groups)
    DB-->>M: AppUser
    M->>A: Principal with tenant_id, groups
    A-->>F: 200 OK { token, email, company, groups }
```

**Explanation:** Authentication uses AWS Cognito for all environments. The user clicks "Sign in with Cognito" and is redirected to Cognito Hosted UI. After authenticating (email/password or SSO via Cognito Identity Providers), Cognito redirects back with an id_token. The frontend stores the token and calls `GET /auth/me`. The backend validates the JWT, provisions or finds the AppUser from Cognito claims (including cognito:groups), and returns user info. Groups (e.g. Admins, Managers, Users) are used for authorization.

### 3.2 Create Order Round Flow

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant C as OrderRoundsController
    participant H as OrderRoundHandler
    participant DB as DbContext

    U->>F: Submit create form
    F->>C: POST /api/v1/orderrounds (Bearer token)
    C->>C: Extract UserId from JWT
    C->>H: CreateAsync(request, userId)
    H->>H: new OrderRound { ... }
    H->>DB: OrderRounds.Add(round)
    H->>DB: SaveChangesAsync
    DB-->>H: round.Id
    H-->>C: OrderRoundResponse
    C-->>F: 201 Created + Location
    F->>F: Navigate to /rounds/:id
```

**Explanation:** The user submits the create-round form. The frontend sends `POST /api/v1/orderrounds` with the JWT in the Authorization header. The controller extracts the user ID from the token and delegates to OrderRoundHandler. The handler creates an OrderRound, saves it to the database, and returns the response. The API responds with 201 Created and a Location header; the frontend navigates to the new round's detail page.

### 3.3 Add Item to Order Round Flow

```mermaid
sequenceDiagram
    participant U as User
    participant F as Frontend
    participant C as OrderRoundsController
    participant H as OrderRoundHandler
    participant DB as DbContext

    U->>F: Submit item form
    F->>C: POST /api/v1/orderrounds/:id/items (Bearer token)
    C->>H: AddItemAsync(orderRoundId, request, userId)
    H->>DB: OrderRounds.FirstOrDefaultAsync
    alt Round not found / closed / deadline passed
        H-->>C: null
        C-->>F: 400 BadRequest
    else Valid
        H->>H: new OrderItem { ... }
        H->>DB: OrderItems.Add(item)
        H->>DB: SaveChangesAsync
        H->>DB: Users.FindAsync (for email)
        H-->>C: OrderItemResponse
        C-->>F: 201 Created
    end
```

**Explanation:** The user adds an item on the order round detail page. The frontend sends `POST /api/v1/orderrounds/:id/items` with the JWT. The handler first checks that the round exists, is Open, and the deadline has not passed. If any check fails, it returns 400. Otherwise, it creates the OrderItem, saves it, and returns 201 Created.

---

## 4. Process Diagrams (Flowcharts)

Flowcharts show decision points and process steps. Diamonds are decisions; rectangles are actions. Arrows show flow direction.

### 4.1 Cognito Registration / Login

```mermaid
flowchart TD
    A[User visits login page] --> B[Redirect to Cognito Hosted UI]
    B --> C[User signs up or signs in]
    C --> D[Cognito returns id_token]
    D --> E[Frontend stores token]
    E --> F[GET /auth/me]
    F --> G[Backend provisions AppUser from Cognito]
    G --> H[User info returned with groups]
```

**Explanation:** Registration and login are handled entirely by AWS Cognito. Users sign up and sign in via Cognito Hosted UI. The frontend receives an id_token, stores it, and calls `GET /auth/me` to provision the AppUser in the backend and get user info (including groups).

### 4.2 Order Round Lifecycle

```mermaid
flowchart TD
    subgraph Creation
        A[User creates round] --> B[OrderRound created]
        B --> C[Status = Open]
    end

    subgraph Active
        C --> D[Users add items]
        D --> E{Deadline passed?}
        E -->|No| D
        E -->|Yes| F[No more items allowed]
    end

    subgraph Closure
        G[Creator closes round] --> H[Status = Closed]
        H --> I[No updates allowed]
    end

    C --> G
    F --> C
```

**Explanation:** An order round starts in **Open** state when created. While open, users can add, update, or remove items until the deadline passes. After the deadline, no new items can be added, but the round stays Open. Only the creator can **close** the round (Status = Closed). Once closed, no updates are allowed.

### 4.3 Add Item Decision Flow

```mermaid
flowchart TD
    A[POST /orderrounds/:id/items] --> B{Round exists?}
    B -->|No| C[404 NotFound]
    B -->|Yes| D{Status = Open?}
    D -->|No| E[400 BadRequest]
    D -->|Yes| F{Deadline not passed?}
    F -->|No| E
    F -->|Yes| G[Create OrderItem]
    G --> H[Save to DB]
    H --> I[201 Created]
```

**Explanation:** Adding an item requires three checks: (1) the round must exist (otherwise 404), (2) the round must be Open (otherwise 400), and (3) the deadline must not have passed (otherwise 400). Only if all pass does the system create the item and return 201.

### 4.4 Authentication Decision Flow

```mermaid
flowchart TD
    A[Request to protected endpoint] --> B{Has Bearer token?}
    B -->|No| C[401 Unauthorized]
    B -->|Yes| D[JWT validation]
    D --> E{Valid?}
    E -->|No| C
    E -->|Yes| F[Extract UserId from claims]
    F --> G[Process request]
```

**Explanation:** Every request to a protected endpoint (e.g. order rounds) must include a valid Bearer token. If missing or invalid, the API returns 401. If valid, the user ID is extracted from the JWT claims and the request proceeds.

---

## 5. Component Diagram

```mermaid
flowchart TB
    subgraph Frontend["Frontend (React)"]
        Login[LoginPage]
        RoundsList[OrderRoundsPage]
        Detail[OrderRoundDetailPage]
        Export[ExportSummaryPage]
        Api[api client]
    end

    subgraph Backend["Backend (ASP.NET Core)"]
        AuthCtrl[AuthController]
        RoundsCtrl[OrderRoundsController]
        Handler[OrderRoundHandler]
        Cognito[Cognito JWT + Provisioning]
    end

    subgraph Data["Data Layer"]
        Db[ApplicationDbContext]
    end

    Login --> Api
    RoundsList --> Api
    Detail --> Api
    Export --> Api
    Api --> AuthCtrl
    Api --> RoundsCtrl
    AuthCtrl --> Cognito
    RoundsCtrl --> Handler
    Handler --> Db
    Cognito --> Db
```

**Explanation:** The component diagram shows how the application is split into layers. The **frontend** has main pages and a shared API client (Axios). Login redirects to Cognito Hosted UI; the API client calls AuthController (`/auth/me`) and OrderRoundsController. The **backend** uses Cognito JWT validation and CognitoUserProvisioningMiddleware to provision AppUser from Cognito claims. The **data layer** uses ApplicationDbContext (EF Core) with a Users table (no ASP.NET Identity).

---

## 6. State Diagram – OrderRound

```mermaid
stateDiagram-v2
    [*] --> Open: Create
    Open --> Open: AddItem / UpdateItem / RemoveItem
    Open --> Open: Update (name, url, deadline)
    Open --> Closed: Update(close=true)
    Closed --> [*]: No further changes
```

**Explanation:** The state diagram shows the lifecycle of an OrderRound. It starts in **Open** when created. While Open, it can receive AddItem, UpdateItem, RemoveItem, and Update (name, URL, deadline). The only transition out of Open is to **Closed**, triggered when the creator sets `close=true`. Once Closed, the round is final and no further changes are allowed.

---

## 7. Deployment / Runtime Flow

```mermaid
flowchart TB
    subgraph Browser
        React[React SPA]
    end

    subgraph Backend["Backend (ASP.NET Core)"]
        RateLimit[Rate Limiter]
        CORS[CORS]
        Auth[Authentication]
        Authz[Authorization]
        AuthCtrl[AuthController]
        RoundsCtrl[OrderRoundsController]
        Handler[OrderRoundHandler]
    end

    subgraph Data
        EF[EF Core]
        PG[(PostgreSQL)]
    end

    React -->|HTTP| RateLimit
    RateLimit --> CORS
    CORS --> Auth
    Auth --> Authz
    Authz --> AuthCtrl
    Authz --> RoundsCtrl
    AuthCtrl --> Handler
    RoundsCtrl --> Handler
    Handler --> EF
    EF --> PG
```

**Explanation:** This diagram shows the runtime path of a request. The React SPA in the browser sends HTTP requests to the backend. Requests pass through **Rate Limiter** (auth endpoints), **CORS** (origin check), **Authentication** (JWT validation), and **Authorization** (role/permission check) before reaching the controllers. The controllers use OrderRoundHandler, which uses EF Core to read and write PostgreSQL. AuthController and OrderRoundsController use the handler; auth is Cognito JWT only.

---

## 8. Request Pipeline (Backend)

```mermaid
flowchart LR
    A[Request] --> B[Serilog Request Logging]
    B --> C[Rate Limiter]
    C --> D[CORS]
    D --> E[Authentication]
    E --> F[Authorization]
    F --> G[Controller]
    G --> H[Response]
```

**Explanation:** The request pipeline shows the order of middleware. Each incoming request passes through: (1) **Serilog** logs the request, (2) **Rate Limiter** enforces limits on auth endpoints, (3) **CORS** validates the Origin header, (4) **Authentication** validates the JWT and sets the user principal, (5) **Authorization** checks permissions, (6) the **Controller** handles the request and returns a response. This order is fixed; e.g. authentication must run before authorization.
