# Room Reference Manifest

This document maps publicly available visual/puzzle references to the three featured starter rooms seeded by `DatabaseSeeder`.

## Clocktower Foyer (Easy)
- Theme intent: bright foyer puzzle, quick key acquisition, direct final-door unlock.
- References:
- https://unsplash.com/photos/a-bunch-of-old-keys-sitting-on-top-of-a-book-7NMHozGQ73M
- https://www.pexels.com/photo/photograph-of-keys-hanging-on-hooks-11105524/
- https://www.pexels.com/photo/close-up-of-aged-brass-key-on-light-surface-31970896/
- Gameplay mapping:
- `note-panel` clue introduces the key-door relationship.
- `key-hook` pickup grants `brass-key`.
- `inventory.use` on `final-door` with `brass-key` completes session.

## Crypt of Echoes (Medium)
- Theme intent: darker crypt flow with a concise multi-step inventory chain and hidden key reveal.
- References:
- https://www.pexels.com/photo/doorway-in-an-ancient-building-made-of-stone-24800114/
- https://pixabay.com/photos/entrance-crypt-mausoleum-doorway-5558774/
- https://pixabay.com/images/search/stone%20crypt/
- Gameplay mapping:
- `rune-wall` clue hints at combining materials.
- `torch-handle` + `oil-flask` -> `lit-torch` via `inventory.combine`.
- `inventory.use` of `lit-torch` reveals `iron-key`.
- `inventory.use` of `iron-key` on `final-gate` completes session.

## Velvet Vault (Hard)
- Theme intent: Art Deco detective-heist escape with a two-stage vault progression and a realtime room transition.
- References:
- https://edgeescaperoom.com/the-four-most-popular-escape-room-themes/
- https://escape-kit.com/en/games/escape-room-themes/
- https://questroom.com/blog/popular-escape-room-themes
- Gameplay mapping:
- Search office cabinets and a door-mounted badge point to gather gadget parts.
- `telescoping-handle` + `badge-magnet` -> `magnetic-retriever` via `inventory.combine`.
- `inventory.use` of `magnetic-retriever` on `floor-vent` reveals `office-key`.
- `inventory.use` of `office-key` on `outer-lock` enables the first door, and inspecting that door transitions the session to the inner vault chamber.
- Search the second room, open a small vault with `vault-key`, and retrieve the final `exit-keycard`.
- `inventory.use` of `exit-keycard` on `final-reader` completes session.

## Notes
- References are design inspiration only; room logic and state machine are implemented locally in the trigger graph definitions stored in room `GraphDefinition`.
- Session time policy is enforced server-side per room metadata, with `Clocktower Foyer` set to 3 minutes, `Crypt of Echoes` set to 5 minutes, and `Velvet Vault` set to 7 minutes.

## Changes Made In This Implementation
- Stabilized player session APIs with explicit, structured error responses (`404/403/400`) for create/join/start/get failures.
- Session duration defaults server-side when metadata is absent, while seeded featured rooms can override via `estimatedMinutes`.
- Added server-side spectator mode for non-host users joining active sessions.
- Added `joinMode` and `canSubmitActions` to session contracts and propagated them to frontend state/UI.
- Enforced spectator restrictions in realtime action submission (`GameHub`) so spectators cannot mutate session state.
- Added featured room metadata support in library API (`isFeatured`, `difficulty`, `estimatedMinutes`) and optional `featured=true` filter.
- Seeded and upserted three canonical playable published rooms: `Clocktower Foyer` (easy), `Crypt of Echoes` (medium), and `Velvet Vault` (hard).
- Implemented richer trigger/inventory/lock progression for both rooms, including final-door completion paths.
- Added a `transitionRoom` trigger effect so a single session can swap from one room layout to another without reconnecting.
- Added payload-aware trigger condition `payloadValueEquals` to strengthen server-side validation of inventory actions.
- Redesigned `/player` front experience with hero section + map selection flow + spectator messaging.
- Updated frontend tests and backend unit/integration tests to cover new flow and behavior.
