---
name: audit-host-client-parity
description: Comprehensive host vs client physics/interaction parity audit - all systems, March 2026
type: project
---

# Host vs Client Parity Audit (2026-03-26)

## Core Asymmetry
All fragment Rigidbodies are kinematic on clients (DisableClientPhysics + FlushKinematicEnforcement).
Host fragments are dynamic after demolition. This means:
- Host player PUSHES debris by walking through it; client player is BLOCKED
- Host player sinks/slides off debris platforms; client stands perfectly
- Vehicle collisions scatter debris on host but not visually on client until sync

## Critical Issues Found
1. **Player-fragment collision parity** - host pushes, client can't. Root: kinematic enforcement on client.
2. **Shard activation not replicated** - DamageHandler.cs:51-55 Activate() with early return = host shard falls, client frozen
3. **Sledgehammer bypasses DamageHandler** - calls rigid.ApplyDamage directly, never un-kinematics stabilized shards, impulse force never fires

## Key Findings
- Interaction detection (SphereCast) works identically - kinematic colliders detected fine
- Fragment carry/deposit works correctly (AddForceSync mechanism)
- Equipped item visual fixed (LateUpdate no longer gated by IsOwner)
- Late-join NetworkObject replication handled by NGO
- Currency is correctly server-authoritative
- Static _claimedFragments persists across sessions (bug)
- InteractionHighlighter may create orphan Canvas on remote players

## Recommended Fix Order
1. Layer-based collision separation for player-fragment parity
2. Fix shard activation replication (remove early return or add RPC)
3. Fix sledgehammer un-kinematic before impulse
4. Clear static _claimedFragments on server spawn
