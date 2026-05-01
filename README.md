# EscapemRooMaker

EscapemRooMaker is a multiplayer escape-room platform with:
- a .NET realtime backend (SignalR + PostgreSQL + Redis)
- a React + TypeScript frontend for players and GM controls
- a trigger/effects engine to process room actions and state transitions

## Project Structure

- `src/backend`: API, realtime hub, application contracts, infrastructure, trigger engine, tests
- `src/frontend`: React player/GM UI, realtime client, Zustand store, UI/canvas components
- `infra`: local Docker services (Postgres, Redis, Azurite)
- `docs`: architecture/contracts/setup notes

## Tech Stack

- Backend: ASP.NET Core (.NET 10), SignalR, EF Core, PostgreSQL, Redis, Serilog
- Frontend: React 19, TypeScript, Zustand, react-konva, Tailwind
- Testing: xUnit (backend), Jest + React Testing Library (frontend)

## What Is Implemented So Far

### Core Realtime Flow
- JWT-authenticated SignalR game hub
- Session join/leave, action submission, snapshot request, and recovery endpoints
- Diff replay on reconnect and snapshot fallback from backend

### State Processing
- Trigger graph validation/evaluation pipeline
- Session snapshot/version persistence and diff sequencing in Redis store
- Built-in GM effects (`gm.hint`, `gm.broadcast`, `gm.reveal`, `gm.force_sync`)

### Frontend Player Experience
- Realtime client with reconnect hooks
- Player screen with room canvas, inventory panel, action feedback panel
- Snapshot + diff application in Zustand store
- Reconnect UX states (`reconnecting`, `recovering`, `replaying`, `synced`)

### Frontend Interaction & Cooldown
- Inventory interaction modes (select/use/combine/clear)
- Local anti-spam cooldown + cooldown chips
- Action error classification including server-rate-limit placeholders (`retryAfterMs`, `policyName`)

### Tests Added
- Store tests for snapshot/diff behavior and snapshot-resync detection
- Player page tests for join/action flow and reconnect banner
- Room canvas and cooldown hook tests
- Backend hub and trigger engine unit/integration tests

## Current Status / Next Major Work

- Rich room composition rendering (assets/layers/hotspots/object states) is still pending.
- Backend structured rate-limit contract is still pending (frontend currently supports fallback parsing).
- Some docs (API/SignalR contract files) are placeholders and need expansion.

## Local Run

### 1) Start infrastructure

```bash
docker compose -f infra/docker-compose.yml up -d
```

### 2) Backend bootstrap (migrations + seed)

```bash
./scripts/bootstrap-day1pm.sh
```

### 3) Run backend

```bash
cd src/backend/EscapeRoom.Api
dotnet run
```

### 4) Run frontend

```bash
cd src/frontend
npm install
npm start
```

## Notes

This repository reflects the current in-progress implementation and is intended to continue through planned phases (room rendering upgrades, stronger backend action contracts, and expanded docs/tests).
