---
status: reverse-documented + aligned with Notion GDD v0.01
source: Assets/Scripts/RayfireNetwork/, Notion GDD
date: 2026-03-26
---

# Destruction System Design

> **Note**: This document was primarily reverse-engineered from the existing
> implementation (the most technically complete system). Notion GDD additions
> include dynamic environment objects and player damage from destruction.

## Overview

The destruction system provides networked, physics-based building demolition using
RayFire for procedural fracturing and a custom host-authoritative replication layer.
Players break structures with tools and vehicles; the host simulates all physics and
broadcasts results to clients via delta-compressed RPCs. Destruction is dangerous —
falling debris, explosions, and collapsing structures can hurt or kill players
(Work Accidents pillar).

## Player Fantasy

Hit a wall with a sledgehammer and watch it shatter. Plant a bomb on a column and
watch the floor above collapse. Drive an excavator through the side of a building
and see the whole structure shift. Every destruction event feels physical, reactive,
and consequential — and lethal if you're standing too close.

## Detailed Rules

### Host-Authoritative Physics Model

All RayFire physics runs exclusively on the host. Clients are visual-only:

| Aspect | Host | Client |
|--------|------|--------|
| Physics simulation | Full (RayFire + PhysX) | None (kinematic) |
| Fragment rigidbodies | Dynamic | Kinematic |
| Demolition triggers | RayFire events fire | Blocked (dmlTp = None) |
| Damage application | Direct (ApplyDamage) | Via ServerRpc only |
| Fragment positions | Authoritative | Interpolated from host |

### Destruction Flow

1. **Damage Application** (host only)
   - Tool/vehicle applies damage to RayfireRigid component
   - RayFire accumulates damage internally
   - When threshold exceeded, RayFire triggers demolition

2. **Demolition Capture** (host)
   - DemolitionReplicator listens to RFDemolitionEvent.GlobalEvent
   - Captures: scene object ID, fragment count, positions, hit point
   - Allocates contiguous ID block from FragmentRegistry
   - Enqueues DemolitionRecord for broadcast

3. **Demolition Broadcast** (host → clients)
   - Flushed every FixedUpdate via DemolishObjectRpc
   - Payload: sceneObjectId, hitPoint, objectPosition/Rotation, fragmentBaseId, count, snapshots

4. **Client Reconstruction**
   - Teleport client object to exact host position (may have moved)
   - Restore dmlTp = Runtime (was set to None by DisableClientPhysics)
   - Execute DemolishForced() to create local fragments
   - Register fragments with deterministic IDs
   - Make all fragments kinematic
   - Enforce kinematic state (batched, once per frame)

5. **Ongoing Transform Sync**
   - Host broadcasts changed fragment positions every 3 fixed frames (~16.7 Hz)
   - Delta compression: only fragments that moved > 0.05 units or rotated > 1°
   - Clients interpolate toward target positions (exponential lerp, speed = 20)

### Fragment ID Assignment (Deterministic)

Scene objects registered at spawn by sorting hierarchy paths alphabetically.
All peers compute identical IDs. New fragments get contiguous ID blocks:

```
Scene objects: [0, 1, 2, ..., N-1]  (sorted by hierarchy path)
First demolition (5 fragments): [N, N+1, N+2, N+3, N+4]
Second demolition (3 fragments): [N+5, N+6, N+7]
```

**Mismatch handling**: If client produces different fragment count than host,
falls back to greedy nearest-position matching. Unmatched client fragments destroyed.

### Late-Join Hydration

New clients receive:
1. Up to 512 past demolition records (replayed in order)
2. Current fragment positions (full state snapshot)

This ensures late joiners see the correct destruction state.

### Dynamic Environment Objects (From Notion GDD — Not Implemented)

Buildings contain interactive environmental systems that affect destruction:

| Object | Location | Effect |
|--------|----------|--------|
| **Boiler Room** | Basement/utility | Explosive — massive area destruction when damaged |
| **Mechanical Room** | Utility areas | Machinery interactions — chain reactions |
| **Electric Cables** | Throughout building | Hazard — shock damage to players |
| **Electric Room** | Utility areas | Power control — affects lighting, other systems |

**Design intent:** These objects reward exploration (Pillar 1: Decision Making).
Finding and strategically triggering environmental objects is more efficient
than brute-force demolition (Pillar 2: Physics). They also create Work Accident
hazards (Pillar 5).

### Player Damage from Destruction (From Notion GDD — Not Implemented)

**Work Accidents pillar** requires:
- Falling debris damages players on impact
- Explosions damage nearby players
- Collapsing floors/walls can crush players
- Electric cables shock players

**Damage sources (proposed):**

| Source | Damage | Knockback | Notes |
|--------|--------|-----------|-------|
| Falling fragment (small) | Low | Minimal | < 1m extent |
| Falling fragment (large) | High | Moderate | > 1m extent, can crush |
| Explosion (bomb range) | High | Strong | Sends player flying |
| Structural collapse | Lethal | Extreme | Floor gives way |
| Electric cable | Medium | Stun | Temporary incapacitation |

## Formulas

### Transform Sync Compression

| Component | Encoding | Bytes |
|-----------|----------|-------|
| Fragment ID | int32 | 4 |
| Position (x,y,z) | float16 each | 6 |
| Rotation | Smallest-3 quaternion (float16 × 3 + 1 byte index) | 7 |
| **Total per fragment** | | **17** |

### Delta Thresholds

```
Position change threshold: sqrMagnitude > 0.0025 (= 0.05 units)
Rotation change threshold: angle > 1 degree
Convergence threshold:     sqrMagnitude < 0.0001 (stop interpolating)
```

### Client Interpolation

```
t = 1 - exp(-interpolationSpeed * deltaTime)
  = 1 - exp(-20 * deltaTime)

newPosition = Lerp(current, target, t)
newRotation = Slerp(current, target, t)
```

### Broadcast Cadence

```
Sync frequency = fixedFrameRate / broadcastEveryNthFixedFrame
               = 50 Hz / 3 = ~16.7 Hz

Bandwidth per sync (100 active fragments):
  100 × 17 bytes = 1,700 bytes per broadcast
  1,700 × 16.7 = ~28 KB/s
```

### Demolition Progress (Proposed — For Stage Completion)
```
progress = totalDestroyedVolume / totalDestructibleVolume * 100%
passThreshold = configurable per stage (e.g., 60%)
```

## Edge Cases

| Edge Case | Solution |
|-----------|----------|
| Client fragment count differs from host | Greedy nearest-position matching + destroy unmatched |
| Object moved on host before demolition | Teleport client object to host position before DemolishForced |
| RayFire connectivity activates neighbors on client | Re-enforce kinematic in batched FlushKinematicEnforcement() |
| Sleeping rigidbodies at new positions | Never skip sleeping RBs in broadcast (delta check handles it) |
| dmlTp=None blocks client demolition | Restore dmlTp=Runtime before DemolishForced |
| Fragment destroyed before sync | PurgeDestroyed() runs every 5 seconds |
| Late-join client | Replay demolition history + send current positions |
| Player disconnect while carrying fragment | OnNetworkDespawn drops fragment, removes from claimed set |
| Damage RPC spam | Rate limited to 50ms per client (20 requests/sec max) |
| Remote damage from impossible distance | Server validates distance < 10 units |
| Player standing under collapsing structure | Damage + knockback from falling fragments |
| Boiler room explosion chain reaction | RayFire connectivity propagates, captured by replicator |
| Electric cable activated during demolition | Shock damage zone, synced via NetworkVariable |
| Many fragments from single explosion (performance) | Delta compression + broadcast cadence limits bandwidth |

## Dependencies

- **RayFire** — Procedural fracturing and physics destruction
- **Unity Netcode for GameObjects** — RPC transport and NetworkVariable sync
- **FragmentRegistry** — Deterministic ID assignment
- **TransformSyncBroadcaster** — Delta-compressed position sync
- **DemolitionReplicator** — Demolition event capture and replay
- **DamageHandler** — Server-side damage validation
- **[Needed] PlayerHealth** — Damage from falling debris, explosions
- **[Needed] DemolitionProgress** — Track destruction % for stage completion
- **[Needed] EnvironmentObject** — Boiler, electrical, mechanical interactions

## Tuning Knobs

| Parameter | Default | Purpose |
|-----------|---------|---------|
| broadcastEveryNthFixedFrame | 3 | Transform sync frequency |
| interpolationSpeed | 20 | Client interpolation rate |
| purgeIntervalSeconds | 5 | Dead fragment cleanup interval |
| maxDamageRange | 10 units | Cheat prevention distance |
| damageRpcCooldown | 0.05s | Rate limit per client |
| MaxHistorySize | 512 | Late-join replay cap |
| MaxCarryableExtent | 2 units | Max debris size for pickup |
| [Needed] debrisDamageThreshold | — | Min fragment mass to hurt player |
| [Needed] explosionPlayerDamage | — | Damage to players in blast radius |
| [Needed] electricShockDamage | — | Damage from electric cables |

## Acceptance Criteria

- [x] Host demolishes object → all clients see identical fragments within 100ms
- [x] Client joining mid-game sees correct destruction state
- [x] 100+ active fragments sync at < 50 KB/s bandwidth
- [x] No physics simulation runs on clients (all kinematic)
- [x] Fragment IDs are deterministic across all peers
- [x] Damage requests from impossible positions are rejected
- [x] Player disconnect does not orphan fragments or corrupt state
- [ ] Falling debris damages players (Work Accidents)
- [ ] Explosions damage nearby players
- [ ] Dynamic environment objects (boiler, electrical) are interactable
- [ ] Boiler room explosion triggers chain reaction
- [ ] Electric cables create shock hazard zones
- [ ] Demolition progress tracked as percentage for stage completion
