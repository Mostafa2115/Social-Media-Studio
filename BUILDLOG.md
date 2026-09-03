# BUILDLOG.md — AI-Usage & Engineering Log

## Project: Social Media Studio (FlyRank Backend Track Capstone)
**Framework:** .NET 10 (ASP.NET Core Web API) + Entity Framework Core  
**Database:** Microsoft SQL Server  
**Real Target:** Telegram Bot API (`sendMessage`)  
**Mock Targets:** MockXPublisher, MockLinkedInPublisher  

---

### Phase 1: Planning & Design
- **Where AI helped:**
  - AI broke down the Capstone Brief requirements and mapped out the 5 core components: Ingestion, Variant Generation with Constraint Profiles, Review Workflow, Adapter Pattern, and Durable Scheduler.
  - Formulated the database schema (BlogPost, PostVariant, ScheduleSlot, PublishAttempt) with unique indexing on `IdempotencyKey`.
- **What I adjusted / decided:**
  - Decided to use **SQL Server** with EF Core instead of SQLite/Postgres to match our preferred stack.
  - Chose **Telegram Bot API** as the real target because it allows free, zero-credit-card posting directly to a channel/group via a simple HTTP POST `sendMessage`.
  - Selected .NET's built-in `BackgroundService` for the durable background worker, removing the need for external queue daemons while remaining 100% crash-resilient through database state transitions (`Pending` -> `Processing` -> `Completed`).

---

### Phase 2: Content Generation & Constraint Validation
- **Where AI helped:**
  - AI drafted the `ConstraintProfile` rules and `ConstraintValidator` with regex checking for hashtags (`#\w+`) and length constraints.
- **Where AI was wrong / What was refined:**
  - Initial variant composer did not strictly budget for hashtag character count inside the 280-character limit for X.
  - Refined the composer logic to calculate `maxAllowedTextLen = 280 - tags.Length` before appending hashtags, guaranteeing that generated tweets never exceed 280 characters.

---

### Phase 3: Review Workflow & Adapter Layer
- **Where AI helped:**
  - Implemented the `ISocialPublisher` interface and three adapter implementations (`TelegramPublisher`, `MockXPublisher`, `MockLinkedInPublisher`).
  - Added strict guard checks in `ReviewWorkflowService`: variants cannot be scheduled unless they have reached `Approved` status.
- **Where AI was wrong / What was refined:**
  - In `PublisherResolver`, the initial string matching was checking strict equality between the configured string and publisher class names. When configuring `"Publishers:Telegram": "MockX"`, it failed to match `"MockXPublisher"` or lowercase `"mock_x"`.
  - Fixed the resolver to normalize strings, strip underscores (`_`), and match prefixes/aliases (`mock_x` -> `MockXPublisher`), allowing effortless configuration swaps.

---

### Phase 4: Durable Scheduling & Idempotency
- **Where AI helped:**
  - Built the `PublishingWorker` background service that polls due slots and executes publishing.
  - Implemented dual-layer idempotency:
    1. Database-level unique constraint on `IdempotencyKey = var_{variantId}_{scheduledTimeUtc}`.
    2. Execution-level check: before triggering the publisher adapter, the worker checks if a successful `PublishAttempt` already exists for the slot. If a worker crashes mid-batch after publishing but before updating the slot, resuming the worker will safely mark the slot completed without double-posting.

---

### Phase 5: Testing & Acceptance Probes
- **Where AI helped:**
  - Generated automated xUnit acceptance tests covering all 6 Probes.
- **Where AI was wrong / What was refined:**
  - The test project was placed inside the repository root, causing the main web `.csproj` to recursively include the test files and produce duplicate assembly compilation errors.
  - Fixed by configuring `<DefaultItemExcludes>` in `Social-Media-Studio.csproj` and adding the test project to the `.slnx` solution file.
  - Ran `dotnet test` and verified that all 6 acceptance probes passed green.
