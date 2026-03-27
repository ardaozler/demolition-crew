# Multiplayer Audit Memory

## Architecture
- Unity Netcode for GameObjects (NGO), host-client model (host is server + client)
- RayFire for destruction physics -- host simulates full physics, clients get KINEMATIC fragments (no client physics)
- Custom destruction sync: DestructionNetworkManager > FragmentRegistry, DemolitionReplicator, TransformSyncBroadcaster, DamageHandler
- Fragment IDs: deterministic at startup via hierarchy path sorting, block-allocated for runtime fragments
- FragmentSnapshot: half-precision position + smallest-three quaternion compression (13 bytes/snapshot)
- New systems: FragmentCarrier (debris pickup), CarryableDebris, DebrisGrinder, VehicleCollisionDamage

## Key Files
- `Assets/Scripts/RayfireNetwork/DestructionNetworkManager.cs` - singleton orchestrator, RPCs, FixedUpdate loop
- `Assets/Scripts/RayfireNetwork/DemolitionReplicator.cs` - host->client demolition sync, local-physics fragment registration
- `Assets/Scripts/RayfireNetwork/TransformSyncBroadcaster.cs` - delta-compressed transform sync, dual-mode interp (hard kinematic / soft physics)
- `Assets/Scripts/RayfireNetwork/FragmentRegistry.cs` - ID registry, localPhysics set, MarkSettled
- `Assets/Scripts/RayfireNetwork/DamageHandler.cs` - server-side damage validation
- `Assets/Scripts/Interaction/EquipmentHandler.cs` - NetworkVariable equip, ServerForceEquip/Unequip
- `Assets/Scripts/Tools/Bomb.cs` + `Detonator.cs` - throw/detonate flow
- `Assets/Scripts/CharacterController/Handlers/MovementHandler.cs` - client-authoritative movement

## Audit Status (2026-03-13, kinematic-client architecture)
- See `audit-findings.md` for full destruction system report
- **Critical:** C1 equipped item not positioned on remote clients (LateUpdate IsOwner guard), C2 two players can carry same fragment (no claim tracking)
- **High:** H1 carried fragments invisible (kinematic=skip in sync), H2 late-join breaks after 512 demolitions, H4 fragment count mismatch leaves invisible debris
- **Medium:** M1 O(n) Contains in ReceiveSnapshots, M2 half-precision limits area to ~500u from origin, M5 drop position not synced immediately
- **Working well:** server-auth damage, deterministic IDs, delta compression, hydration snapshots, kinematic enforcement, TrySetParent for equip race

## Player Carry/Throw Audit (2026-03-26)
- See `audit-player-carry.md` for details
- **Critical:** C1 dual LateUpdate positioning fight, C2 throw force server/owner desync, C3 no input disable while carried
- **High:** H1 NT enable/disable race, H2 drop position from stale server pos, H3 server modifies owner-auth RB, H4 no defensive NT re-enable
- Key pattern: owner-authoritative NT + server-side physics manipulation = desync. Physics ops for carried players must go through owner RPCs.

## Host vs Client Parity Audit (2026-03-26)
- See `audit-host-client-parity.md` for comprehensive 16-system report
- **Critical:** Host pushes debris (dynamic RB), client blocked (kinematic RB) -- core kinematic asymmetry
- **High:** Shard Activate() not replicated (DamageHandler:51-55), Sledgehammer bypasses DamageHandler (no un-kinematic, impulse never fires)
- **Medium:** Static _claimedFragments persists across sessions, InteractionHighlighter orphan Canvas, no client sledgehammer feedback
- **Fixed since last audit:** C1 EquipmentHandler LateUpdate no longer gated by IsOwner, C2 _claimedFragments claim tracking added
- Interaction detection (SphereCast) works fine with kinematic colliders
- Fragment carry/deposit flow works correctly via AddForceSync
- Standing on debris: host sinks/slides, client stands perfectly (kinematic asymmetry)

## Player Setup Audit (2026-03-13)
- See `audit-player-setup.md` for details
- NetworkPlayerSetup disables components in OnNetworkSpawn but Start/Awake run first -> cursor lock, UI canvas, input map all fire for remote players briefly
- No disconnect cleanup for equipped/carried items
- Client-authoritative movement (no server validation)
- No connection approval, no max player cap, no connection failure UI
- ServerListenAddress 127.0.0.1 in NetworkManager prefab won't accept LAN/WAN connections
- NetworkTransform AuthorityMode: 1 (Owner) -- matches client-auth movement pattern
- Player prefab: DontDestroyWithOwner=0 (correct), Ownership=1 (correct for player prefab)
