# Room Rendering + Realtime Sync + Rate Limiting Update

## Summary
This update closes remaining gaps across:
- Phase 1: richer inventory model and state diff application
- Phase 2: reconnect recovery flow parity between Player and GM
- Phase 3: policy-aware server rate limiting and cleaner cooldown UX

## Frontend changes
- Expanded `InventoryItem` with metadata fields:
  - `type`, `stack`, `status`, `usableTargetIds`, `combinableWithIds`
- Added normalization support for both legacy/simple and rich inventory payloads.
- Updated `InventoryPanel` to display item metadata and enforce metadata-aware affordances.
- Added typed inventory action payloads:
  - `InventoryUseActionPayload`
  - `InventoryCombineActionPayload`
- Added shared reconnect recovery controller used by both Player and GM pages.
- Updated Player action error handling so structured server rate-limit errors stay in action feedback and do not show as generic connection errors.
- Updated room targeting logic to include selected item metadata and status.

## Backend changes
- Extended `StateDiffEnvelope` with optional:
  - `StatePatch`
  - `FullStateJson`
- Added `StateDiffPayloadBuilder` to emit room/inventory patch payloads for known mutations and fallback to full-state JSON for unknown non-message mutations.
- Wired patch/full-state emission into `SessionActionProcessor`.
- Added policy-aware rate limiting context and decision fields:
  - `PolicyScope`
  - `ActionKey`
- Updated in-memory limiter to evaluate by scope/role/action key and support separate GM policy behavior.
- Updated hub integration to pass policy context and return richer structured rate-limit payloads.
- Added appsettings config for separate Player and GM rate-limit policies.

## Test coverage
### Frontend
- Inventory normalization tests for legacy and rich payloads.
- Reconnect recovery controller tests for:
  - replay path
  - explicit `snapshotSent`
  - snapshot fallback
  - recovery failure fallback behavior
- Player page test for structured rate-limit error handling.
- Action error parser tests for structured and unstructured errors.

### Backend
- Diff payload builder tests for inventory patch, room patch, and full-state fallback.
- Hub tests for:
  - structured rate-limit behavior
  - GM scope rate-limit evaluation
  - recovery replay and snapshot paths
- In-memory rate limiter tests for player cooldown behavior and GM bypass behavior.

## Outcome
The app now relies less on snapshot-only recovery by supporting richer diffs, has consistent reconnect lifecycle behavior across Player/GM surfaces, and applies rate limiting in a policy-aware way that better matches gameplay and admin workflows.
