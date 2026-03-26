---
name: audit-findings
description: Full multiplayer audit report for Demolition Crew - Option B local-physics architecture, NGO host-client
type: project
---

# Multiplayer Audit Report - Demolition Crew
**Date:** 2026-03-12 (fourth pass, Option B local-physics architecture)
**Verdict:** Needs Fixes Before Ship

## Previous Finding Status
- Late-join hydration: FIXED - sends demolition history + full transform snapshot
- ProjectileLauncher/DebugPanel: FIXED - compile-gated
- RequestHydrationRpc spam: FIXED - _hydratedClients one-shot guard
- UseServerRpc stale usable: FIXED - derives from equippedItemRef.Value directly
- Demolition history unbounded: PARTIALLY FIXED - capped at 512 (but eviction breaks late-join, see H4)
- Zero aim direction: FIXED
- Fragment matching: FIXED - index + proximity fallback
- Disconnect cleanup: FIXED
- GC pressure: FIXED - buffer reuse
- Bomb destroy delay: FIXED

## Current Issues

### C1: Equipped item not positioned on remote clients
- **File**: EquipmentHandler.cs lines 195-203
- LateUpdate holdPoint snapping only runs on IsOwner
- Remote players see tools at player root, not in hand
- Fix: remove IsOwner guard or parent to holdPoint directly

### H1/M4: Fragment velocity mismatch at demolition
- **File**: DemolitionReplicator.cs lines 237-250
- After DemolishForced, fragments snapped to host position but velocity not zeroed
- Fragments fly in wrong directions then rubber-band back
- Fix: zero rb.linearVelocity/angularVelocity after snap

### H2: MovePosition fights local physics
- **File**: TransformSyncBroadcaster.cs lines 156-170
- Soft correction via MovePosition on non-kinematic RBs causes jitter
- Fix: use velocity-based correction or additive force instead

### H3: Premature MarkSettled based on client sleep state
- **File**: TransformSyncBroadcaster.cs lines 186-190
- Client fragment goes kinematic when sleeping + converged, but host may still be moving
- Fix: gate on host stop-sending signal, not client sleep state

### H4: Demolition history eviction breaks late-join
- **File**: DemolitionReplicator.cs lines 86-88
- After 512 demolitions, early records evicted; late-joiner sees intact objects
- Fix: track demolished IDs separately (never evict) or raise cap

### M1: O(n) contains in ReceiveSnapshots
- **File**: TransformSyncBroadcaster.cs line 127
- List.Contains is O(n) per snapshot; add HashSet for O(1)

### M2: RemoveAt(0) is O(n) on demolition history
- **File**: DemolitionReplicator.cs line 88
- Use circular buffer instead

### M6: Client-authoritative movement (acceptable for co-op)

## Prioritized Action List
1. C1 - Fix equipped item visual on remote clients
2. H1/M4 - Zero fragment velocity after snap
3. H2 - Fix soft correction method for physics fragments
4. H3 - Fix premature MarkSettled
5. H4 - Fix late-join for long sessions
6. M1 - Add HashSet for active target lookups
