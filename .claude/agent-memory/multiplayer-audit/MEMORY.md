# Multiplayer Audit Memory

## Architecture
- Unity Netcode for GameObjects (NGO), host-client model (host is server + client)
- RayFire for destruction physics -- only host simulates physics, clients are kinematic
- Custom destruction sync system: DestructionNetworkManager (singleton NetworkBehaviour) orchestrates FragmentRegistry, DemolitionReplicator, TransformSyncBroadcaster
- Fragment IDs assigned deterministically at startup via hierarchy path sorting, then block-allocated for runtime fragments

## Key Files
- `Assets/Scripts/RayfireNetwork/DestructionNetworkManager.cs` - singleton network manager, RPCs, FixedUpdate loop
- `Assets/Scripts/RayfireNetwork/DemolitionReplicator.cs` - queues demolitions on host, executes on client via greedy nearest-position matching
- `Assets/Scripts/RayfireNetwork/TransformSyncBroadcaster.cs` - delta-compressed transform sync every Nth fixed frame
- `Assets/Scripts/RayfireNetwork/FragmentRegistry.cs` - deterministic ID registry, sorted by hierarchy path
- `Assets/Scripts/RayfireNetwork/FragmentSnapshot.cs` - half-precision position + smallest-three quaternion (13 bytes/fragment)
- `Assets/Scripts/Tools/Bomb.cs` + `Detonator.cs` - throw bomb, swap to detonator, remote detonate
- `Assets/Scripts/Tools/Sledgehammer.cs` - server-authoritative melee tool
- `Assets/Scripts/Interaction/EquipmentHandler.cs` - NetworkVariable-based equip system
- `Assets/Scripts/TEST/ProjectileLauncher.cs` - client-authoritative projectile launcher (test tool)
- `Assets/Scripts/CharacterController/Handlers/MovementHandler.cs` - client-authoritative movement (owner only)

## Known Patterns
- Clients have RayFire demolition disabled (dmlTp = None), re-enabled per-object when DemolishObjectRpc arrives
- Fragment matching uses greedy nearest-position O(n*m) -- works for small counts, risky for large
- Transform sync uses delta compression with position threshold 0.05 units and rotation threshold 1 degree
- Broadcast rate: every 3rd FixedUpdate (~16.67 Hz at 50Hz physics)
- Purge cycle: every 5 seconds for destroyed fragments
- EnforceClientKinematic() added after DemolishForced to catch RayFire connectivity activations

## Audit Status
- Full audit completed 2026-03-11 (updated). See `audit-findings.md` for detailed report.
- Top blocker: no late-join state hydration (C1)
- RequestDamageRpc validation is solid (rate limit, distance check, server-determined damage)
- EquipmentHandler has rate limit on equip but NOT on UseServerRpc
- Bomb.Detonate has `detonated` bool guard against double-detonation
- Detonator.OnUnequip missing null check on linkedBomb (crash risk)
- ProjectileLauncher is test-only but not gated behind any debug flag
