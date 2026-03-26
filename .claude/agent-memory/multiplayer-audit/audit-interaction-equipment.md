---
name: Interaction & Equipment Audit Findings
description: Multiplayer audit of EquipmentHandler, FragmentCarrier, Bomb/Detonator flow, and tool IUsable implementations (2026-03-13)
type: project
---

## Audit Date: 2026-03-13

### Critical Issues
- C1: EquipmentHandler.LateUpdate has IsOwner guard -- equipped items not positioned on remote clients
- C2: FragmentCarrier.PickupServerRpc has no cross-player claim check -- two players can carry same fragment

### High Issues
- H1: Detonator can be picked up by another player after drop, detonating someone else's bomb (or detonating bomb held by another player)
- H2: ServerForceEquip doesn't drop existing equipment first -- can create ghost items parented but untracked
- H3: Player disconnect while carrying debris leaves fragment permanently kinematic/invisible
- H4: Player disconnect while holding tool leaves it parented to despawning player (item loss)

### Medium Issues
- M1: DropItemClientRpc runs on all clients including host, physics may diverge without NetworkTransform on tools
- M3: FragmentCarrier RPCs have no rate limiting

### Architecture Notes
- EquipmentHandler: NetworkVariable<NetworkObjectReference>, TrySetParent for race-safe equip, rate-limited RPCs
- FragmentCarrier: NetworkVariable<int> for fragment ID, non-NetworkObject fragments from FragmentRegistry
- Bomb->Detonator: ServerForceUnequip bomb, spawn+Spawn detonator, SetBomb, ServerForceEquip detonator
- Tools (Sledgehammer, Bomb, Detonator): IUsable + IInteractable
- CarryableDebris: IInteractable only, dynamically added to small fragments post-demolition
- DebrisGrinder: scene-placed IInteractable deposit point
- VehicleCollisionDamage: MonoBehaviour with NetworkManager.Singleton server check (not NetworkBehaviour)
- RocketLauncher: empty stub
