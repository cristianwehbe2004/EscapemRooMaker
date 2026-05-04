# API Contracts

## Player Sessions

Player sessions support hybrid access. Authenticated players are identified from their bearer token; guests send a display name and a generated `guestActorId`.

### Create Hosted Session

`POST /api/player/sessions`

```json
{
  "roomId": "optional-published-room-uuid",
  "durationMinutes": 60,
  "displayName": "Player"
}
```

Creates a pending hosted session. The timer does not begin until `start` is called.

### Quick Start

`POST /api/player/sessions/quick-start`

Uses the same request body as hosted creation. Creates an active private session and starts the timer immediately.

### Join Session

`POST /api/player/sessions/{sessionId}/join`

```json
{
  "displayName": "Player",
  "guestActorId": "guest-..."
}
```

Validates the session and returns lobby/game metadata.

### Start Session

`POST /api/player/sessions/{sessionId}/start`

Starts a pending hosted session and writes a fresh realtime snapshot.

### Get Session

`GET /api/player/sessions/{sessionId}?displayName=Player&guestActorId=guest-...`

Returns current status and timer metadata.

### PlayerSessionSummary

```json
{
  "sessionId": "uuid",
  "roomId": "uuid",
  "roomName": "Vault Puzzle",
  "status": "Pending | Active | Completed | Cancelled | Expired",
  "durationMinutes": 60,
  "startedAtUtc": "2026-05-04T00:00:00Z",
  "endedAtUtc": null,
  "endsAtUtc": "2026-05-04T01:00:00Z",
  "serverTimeUtc": "2026-05-04T00:00:00Z",
  "remainingSeconds": 3600,
  "isQuickPlay": false,
  "playerJoinPath": "/player?sessionId=...",
  "gmJoinPath": "/gm?sessionId=...",
  "actorId": "guest-...",
  "displayName": "Player"
}
```
