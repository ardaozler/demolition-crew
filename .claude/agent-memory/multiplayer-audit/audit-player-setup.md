---
name: Player Setup Audit Findings (2026-03-13)
description: Multiplayer audit of NetworkPlayerSetup, player prefab, connection handling, camera/input isolation
type: project
---

## Audit scope
NetworkPlayerSetup.cs, Player.prefab, NetworkManager.prefab, NetworkMenuUI.cs, MovementHandler, JumpHandler, GravityHandler, InputProvider, FirstPersonCamera, CameraInputProvider, InteractionDetector, InteractionHighlighter, EquipmentHandler, FragmentCarrier, CarryableDebris, DebrisGrinder.

## Key findings
- C1: Equipped items invisible on remote clients (LateUpdate IsOwner guard) -- already tracked
- H1: No connection failure UI -- client hides menu immediately on StartClient, never shows error if connection fails
- H2: No connection approval -- anyone can connect, no player cap
- H3: No disconnect cleanup for equipped items / carried fragments
- H4: Client-authoritative movement with no server validation
- M1: FirstPersonCamera locks cursor for ALL instances including remote (runs in Start, before OnNetworkSpawn disables it)
- M2: InteractionHighlighter creates UI canvas for remote players (Start runs before OnNetworkSpawn)
- M3: InputProvider enables action map for remote players briefly (Awake enables, OnNetworkSpawn disables)
- M4: GroundDetector runs physics queries on all clients for all players
- L1: NetworkMenuUI has no connection timeout/feedback
- L2: No spawn point randomization
- L3: ServerListenAddress is 127.0.0.1 (won't accept external connections out of box)

**Why:** These will cause visible bugs in playtests over real networks.
**How to apply:** Priority order: H3 (data loss), H4 (exploitable), H1 (broken UX), M1/M2 (visual bugs on join), then remaining.
