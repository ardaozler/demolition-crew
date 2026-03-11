# Multiplayer Audit Findings - 2026-03-11

## Verdict: Needs Fixes Before Ship

### Critical Issues
1. No late-join state hydration for destruction
2. Client-authoritative aim vectors (EquipmentHandler UseServerRpc) - exploitable
3. ProjectileLauncher is fully client-authoritative (test tool, not gated)
4. Bomb.Detonate() can NPE / double-despawn under race conditions

### High Issues
1. RequestDamageRpc has no validation (damage param from client, range check missing)
2. No rate limiting on any client->server RPCs
3. Bomb thrown state not synced via NetworkVariable (desync on late join)
4. Detonator linkedBomb is local reference, not synced
5. Fragment count scaling: O(n) broadcast per tick with no spatial partitioning

### Medium Issues
1. Fragment matching is greedy nearest-position O(n*m) - can mismatch with many fragments
2. Half-precision positions lose accuracy beyond ~1km
3. EquipmentHandler allows equipping any NetworkObject (no type/distance server validation)
4. _lastSent dictionary in TransformSyncBroadcaster grows without bound for sleeping fragments
5. PurgeStale allocates List<int> every 5 seconds

### Low Issues
1. MoveVisualProjectile on remote clients drifts from physics sim
2. Quaternion compression edge case with near-zero largest component
