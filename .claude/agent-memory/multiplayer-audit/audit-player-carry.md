---
name: Player Carry/Throw System Audit
description: Multiplayer audit of PlayerCarrier carry/throw system - host vs client discrepancies, physics authority conflicts, input disabling gaps
type: project
---

## Player Carry/Throw Audit (2026-03-26)

### Core Pattern
- PlayerCarrier uses two NetworkVariables: _carriedPlayerRef (who I carry), _myCarrierRef (who carries me)
- Pickup/drop/throw via ServerRpcs with server-side validation
- LateUpdate dual-positioning: carrier positions carried player on all machines AND owner snaps to carrier's carryPoint
- NetworkTransform disabled while carried, re-enabled on release
- Throw force sent via SendTo.Owner RPC to work with owner-authoritative NT

### Critical Issues Found
- **C1 Dual LateUpdate fight**: Both carrier and carried-owner write position in LateUpdate on the carried player's machine. Causes jitter.
- **C2 Throw force path**: Server calls RestorePhysics (zeroes velocity) then owner RPC applies throw. Third-party observers see snap because server has zero velocity while owner has throw velocity.
- **C3 No input disable while carried**: MovementHandler, FirstPersonCamera, JumpHandler all still active on carried player. Camera rotation desyncs from what others see.

### High Issues
- H1: NT re-enable/disable race during pickup/release (one-tick gap)
- H2: Drop position calculated from server's stale copy of carrier position (owner-auth NT)
- H3: RestorePhysics modifies server-side RB state for owner-auth players (no effect, creates brief desync)
- H4: RestorePhysics doesn't re-enable NT (relies on NetworkVariable callback)

### Working Correctly
- Server-side validation in CarryServerRpc (distance, mutual exclusivity, carried/equipment checks)
- TrySetParent not needed here (uses NetworkVariable refs instead)
- OnNetworkDespawn cleanup for both carrier and carried disconnects
- Late-join hydration resolves existing carry state
