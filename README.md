# Social Media Studio — Multi-Platform Campaign Publisher

> Capstone project for the **FlyRank Backend Track**.  
> Converts blog posts into platform-tailored social campaigns with strict constraint enforcement, review workflows, durable scheduling, zero-duplicate idempotency guarantees, and swappable adapter architecture.

---

## Architecture Overview

```
[ Blog Post: URL or Markdown ]
              │
              ▼
    [ Ingest & Store ] ───► [ Single Source of Truth: SQL Server ]
              │
              ▼
   [ Variant Generator ] ───► [ Constraint Profiles Validator ]
                               (Length, Tone, Hashtag Rules)
                                      │
                                      ▼
                             [ Review Workflow ]
                             (Draft -> Approved | Rejected)
                                      │
                                      ▼
                        [ Durable Background Scheduler ]
                         (Resilient Worker + DB State)
                                      │
                                      ▼
                         [ ISocialPublisher Interface ]
                                      │
          ┌───────────────────────────┼───────────────────────────┐
          ▼                           ▼                           ▼
[ TelegramPublisher (Real) ]  [ MockXPublisher ]     [ MockLinkedInPublisher ]
          │                           │                           │
          └───────────────────────────┼───────────────────────────┘
                                      │
                                      ▼
                         [ Idempotent Publish History ]
                          (One Slot = One Post, Always)
```

---

## Core Features & Guarantees

1. **Single Source of Truth (Ingestion):**
   - Ingests blog posts from raw text/Markdown or extracts clean content from URLs.
   - All variants are strictly derived from the stored source post.

2. **Platform Constraint Profiles:**
   - **X (Twitter):** Max 280 chars, 1–3 hashtags, punchy high-impact tone.
   - **LinkedIn:** Max 3000 chars, 2–5 hashtags, professional structure with key takeaways.
   - **Telegram:** Max 4096 chars, rich markdown format with call-to-action.
   - **Strict Enforcement:** Any variant that violates a constraint profile is blocked immediately with an explicit error message naming the violated rule (`MaxLengthExceeded`, `MinHashtagsNotMet`, etc.). Rule-breaking variants never reach review.

3. **Review & Approval Workflow:**
   - Variants transition through: `Draft` ➔ `Approved` / `Rejected` ➔ `Published`.
   - **Security Guardrail:** Only variants in `Approved` status can be scheduled. Scheduling an unapproved variant immediately returns `400 Bad Request`.

4. **Swappable Adapter Architecture:**
   - Built on the `ISocialPublisher` interface.
   - **Real Target:** `TelegramPublisher` (uses free Telegram Bot API `sendMessage`).
   - **Mock Targets:** `MockXPublisher` and `MockLinkedInPublisher` for safe simulated publishing.
   - **Config-Driven Swapping:** Change the active adapter in `appsettings.json` (e.g., redirect Telegram to `MockX`) without modifying a single line of business logic.

5. **Durable Scheduling & Idempotency:**
   - Background worker (`DurablePublishingWorker`) periodically polls due slots.
   - Deterministic `IdempotencyKey` per variant and scheduled time slot.
   - **Crash Recovery:** If the worker crashes or restarts mid-batch, it resumes safely. The system verifies previous publish attempts, guaranteeing **zero duplicate posts**.

---

## Quickstart & How to Run

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Microsoft SQL Server (or SQL Server LocalDB / Docker container)

### 1. Configuration
Copy the example configuration:
```bash
cp appsettings.example.json appsettings.json
```
Edit `appsettings.json` with your SQL Server connection string and optional Telegram credentials:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SocialMediaStudioDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Telegram": {
    "BotToken": "YOUR_TELEGRAM_BOT_TOKEN",
    "ChatId": "@YOUR_CHANNEL_OR_CHAT_ID"
  }
}
```
*(Note: If Telegram credentials are left default, the publisher automatically operates in safe simulation mode with mock URLs).*

### 2. Run the Service
Run with a single command:
```bash
dotnet run
```
The API boots up on `https://localhost:7194` / `http://localhost:5246` with interactive OpenAPI documentation available at `/openapi/v1.json`.

### 3. Run Automated Tests
All 6 acceptance probes can be verified with:
```bash
dotnet test
```

---

## API Endpoints Reference

### Ingestion & Variants
- `POST /api/posts` — Ingest a new post (URL or Markdown).
- `GET /api/posts` — List all stored posts.
- `POST /api/posts/{id}/generate-variants` — Generate platform-tailored variants.
- `POST /api/posts/{id}/variants` — Create a custom variant with constraint validation.
- `GET /api/posts/{id}/variants` — List variants for a post.

### Review Workflow
- `POST /api/variants/{id}/approve` — Approve variant for scheduling.
- `POST /api/variants/{id}/reject` — Reject variant with reason.
- `PUT /api/variants/{id}` — Edit variant content (triggers re-validation).
- `POST /api/variants/{id}/schedule` — Schedule an approved variant (Rejects unapproved with 400).

### Scheduling & History
- `GET /api/schedule/slots` — View scheduled slots and status.
- `POST /api/schedule/process-due` — Trigger processing of due slots.
- `GET /api/history` — View full publish history and audit logs.

---

## Known Limitations

- **Source Image Pipelines:** Image generation and auto-cropping (e.g., 1080x1080 and 1600x900) are reserved for Capstone 1; this capstone focuses purely on reliable text publishing, constraint enforcement, and distributed scheduling.
- **Single-Worker Polling:** The durable worker currently uses polling with DB state locking. In a high-throughput multi-cluster production environment, distributed locks (such as Redis Redlock or SQL Server application locks) would be recommended to prevent race conditions across multiple replica instances.
