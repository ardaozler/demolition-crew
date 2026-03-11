# Multiplayer Audit Findings - 2026-03-11 (Updated)

## Verdict: Needs Fixes Before Ship

## Previous Finding Status
- RequestDamageRpc validation: FIXED (server-side damage, rate limit, distance check all present)
- Rate limiting on RPCs: PARTIALLY FIXED (damage RPC and equip RPC have rate limits; UseServerRpc/StopUseServerRpc/UnequipServerRpc do not)
- Bomb thrown state: FIXED (uses NetworkVariable<bool>)
- EnforceClientKinematic after DemolishForced: FIXED (just added)

---

## Critical Issues

### C1: No Late-Join State Hydration
- **Files**: DestructionNetworkManager.cs, DemolitionReplicator.cs, FragmentRegistry.cs
- **Issue**: When a client joins mid-game, RegisterSceneObjects() runs identically on host and client. But objects already demolished on the host still exist on the joining client. The client registers them and assigns the same base IDs, but the host has already unregistered those IDs and allocated runtime fragment IDs in their place. The client's registry will contain stale scene objects that no longer exist on the host, and will be missing all runtime fragments.
- **Failure**: Late-joining client sees intact buildings that were already destroyed. Transform sync RPCs reference fragment IDs the client doesn't have. Demolition RPCs for already-destroyed objects are never sent again.
- **Recommendation**: On client connect, send a full state snapshot: which scene objects are destroyed, and the current registry of runtime fragments with their positions. Alternatively, use a "catchup" RPC that replays all demolition records.

### C2: ProjectileLauncher is Fully Client-Authoritative
- **File**: ProjectileLauncher.cs, lines 39-58
- **Issue**: Clients spawn physics projectiles locally and send ShootRpc(origin, direction) to other clients (NotOwner). The host never validates the origin or direction. The RFBreaker component does collision detection and calls manager.RequestDamageRpc, which is server-validated, but the projectile trajectory itself is entirely client-determined.
- **Exploit**: A cheating client can fire projectiles from any origin in any direction at any rate. The fireRate check (line 44) is client-side only. The damage itself goes through RequestDamageRpc which has server validation, but the projectile visuals are unvalidated and can be spammed.
- **Recommendation**: If this is a test tool, gate it behind a debug flag. If shipping, move fire rate validation to server and validate origin against player position.

### C3: Detonator.OnUseStarted Race Condition with Despawn
- **File**: Detonator.cs lines 28-44, Bomb.cs lines 110-140
- **Issue**: In Detonator.OnUseStarted, the sequence is: ServerForceUnequip -> bomb.Detonate() -> NetworkObject.Despawn(). Inside Bomb.Detonate(), the bomb despawns itself (line 139). If the detonator holder reference in Bomb was already cleared (e.g., the detonator was briefly unequipped and re-equipped by exploit), Detonate() proceeds but detonatorHolder is null. More critically: ServerForceUnequip triggers OnEquippedItemChanged on all clients, which calls OnUnequip on the Detonator. OnUnequip calls linkedBomb.ClearDetonatorRef(). But linkedBomb was already set to null on line 34. This is fine. However, there is a timing issue: if two players somehow both have references to the same bomb (possible if a second player picks up the detonator after it's dropped and a new bomb is thrown), the bomb could be detonated twice or the wrong bomb detonated.
- **Recommendation**: Add a null check on NetworkObject before Despawn in Detonator. The `detonated` flag in Bomb.Detonate() already prevents double-detonation, which is good. Ensure the detonator can only be used by the player who threw the bomb.

---

## High Issues

### H1: EquipmentHandler UseServerRpc Has No Rate Limit
- **File**: EquipmentHandler.cs, lines 160-169
- **Issue**: UseServerRpc has no rate limiting. A malicious client can spam use actions every frame. The sledgehammer has its own cooldown (settings.Cooldown), but a crafted client could bypass the EquipmentHandler and send UseServerRpc directly at any rate.
- **Exploit**: Rapid-fire sledgehammer swings bypassing the cooldown, or rapid bomb throws.
- **Recommendation**: Add server-side rate limiting to UseServerRpc similar to the equip rate limit.

### H2: Aim Direction Not Normalized Server-Side for Sledgehammer
- **File**: EquipmentHandler.cs line 169, Sledgehammer.cs line 36
- **Issue**: UseServerRpc normalizes aimDirection (line 169), which is good. But the normalized direction is used in a Physics.Raycast. A client could send a zero vector, which after normalization is still zero, causing the raycast to have no direction. This is a minor edge case but should be checked.
- **Recommendation**: Reject aimDirection with near-zero magnitude before normalization.

### H3: Fragment Count Scaling - O(N) Broadcast Every Tick
- **Files**: TransformSyncBroadcaster.cs, DestructionNetworkManager.cs
- **Issue**: CaptureHostState iterates all registered fragments every broadcast tick (~16.67Hz). With delta compression, only changed fragments are sent, but the iteration itself is O(N). With hundreds of active fragments after multiple demolitions, this could cause frame spikes on the host.
- **Exploit/Failure**: In a game with many demolished objects, the host's FixedUpdate becomes expensive. The snapshot array allocation (line 63 ToArray()) also creates GC pressure every broadcast.
- **Recommendation**: For now this is acceptable for small-to-medium fragment counts (<500). For scaling: consider spatial partitioning, or maintaining a "dirty" set of fragments that moved, rather than iterating all. Replace ToArray() with a pooled array.

### H4: EnforceClientKinematic Iterates Entire Registry
- **File**: DemolitionReplicator.cs, lines 140-151
- **Issue**: EnforceClientKinematic iterates ALL registered fragments after every demolition. With 500+ fragments, this is O(N) per demolition event. If multiple demolitions arrive in quick succession (e.g., bomb explosion demolishes 10 objects), this runs 10 times in the same frame.
- **Recommendation**: Track which fragments were activated by RayFire's connectivity system (subscribe to activation events) rather than brute-force iterating. Or batch demolitions so EnforceClientKinematic runs once per frame.

### H5: _lastSent Dictionary Never Purged for Sleeping/Removed Fragments
- **File**: TransformSyncBroadcaster.cs
- **Issue**: _lastSent stores the last broadcast position for every fragment ever sent. PurgeStale (line 104) removes from _targets and _lastSent only if the fragment is gone from the registry. But fragments that are still registered but sleeping will accumulate in _lastSent forever. This is a slow memory leak.
- **Recommendation**: PurgeStale already handles this for destroyed fragments. The remaining concern is fragments that become kinematic (settled). Consider removing from _lastSent when a fragment goes kinematic, or cap the dictionary size.

---

## Medium Issues

### M1: Greedy Fragment Matching Can Mismatch
- **File**: DemolitionReplicator.cs, lines 157-207
- **Issue**: The greedy nearest-position matching algorithm matches host fragments to client fragments by proximity. This works well when fragments are well-separated, but for a symmetric object demolished near its center, multiple fragments may be at similar positions, and the greedy order (iterating host fragments in order) can cause cross-assignments.
- **Failure**: Fragment A on host gets matched to Fragment B on client and vice versa. The visual result is fragments that teleport to wrong positions when transform sync arrives.
- **Recommendation**: For small fragment counts this is acceptable. For robustness, consider using the Hungarian algorithm or at minimum sorting both lists by distance from center before matching. Alternatively, since RayFire produces fragments in a deterministic order from the same mesh, consider matching by index if the fragment counts are equal.

### M2: Half-Precision Position Limits World Size
- **File**: FragmentSnapshot.cs
- **Issue**: half (float16) has a range of ~65504 with decreasing precision. At positions beyond ~100 units, precision drops to ~0.1 units. At ~1000 units, precision is ~1 unit. At ~10000 units, precision is ~10 units.
- **Recommendation**: If the game world is within 200x200 units, this is fine. Document the world size constraint. For larger worlds, use relative-to-origin encoding.

### M3: Bomb Throw Physics Desync on Clients
- **File**: Bomb.cs, lines 62-91
- **Issue**: When a bomb is thrown, the server sets rb.isKinematic = false and applies force. The ThrowRpc (line 91) is sent to all clients but is currently a stub (no physics replication). The bomb's NetworkObject presumably has a NetworkTransform, but if it doesn't, clients won't see the bomb move after being thrown.
- **Recommendation**: Verify the Bomb prefab has a NetworkTransform component. If not, the bomb trajectory will only be visible on the host.

### M4: EquipmentHandler.OnEquippedItemChanged SetItemPhysics on Old Item
- **File**: EquipmentHandler.cs, lines 189-208
- **Issue**: When equipped item changes, OnEquippedItemChanged re-enables physics on the old item (SetItemPhysics enabled:true). This runs on ALL clients (NetworkVariable callback). For the bomb flow: when ServerForceUnequip is called during throw, the old item (bomb) gets SetItemPhysics(enabled:true) on all clients. But the server already set rb.isKinematic = false on line 69 of Bomb.cs. If OnEquippedItemChanged fires BEFORE the throw physics are applied, it could interfere. Since NetworkVariable changes are applied in the same frame on the server, this should be fine, but the ordering between NetworkVariable callbacks and the rest of OnUseStarted is fragile.
- **Recommendation**: Verify the execution order. ServerForceUnequip sets equippedItemRef.Value which triggers the callback synchronously on the server. The callback enables physics on the bomb, then the throw code also enables physics. This is redundant but not harmful. However, on clients, the callback may fire at a different time relative to the ThrowRpc.

### M5: No Validation on StopUseServerRpc
- **File**: EquipmentHandler.cs, lines 172-178
- **Issue**: StopUseServerRpc checks that equippedItemRef has a value, but has no rate limit and no sender validation. A client could spam this RPC. The impact is low since OnUseStopped is typically a no-op for current tools.
- **Recommendation**: Add rate limiting for consistency, or document that this is intentionally unthrottled.

### M6: Destroyed Bomb Leaves Orphaned Detonator References
- **File**: Bomb.cs, lines 110-140
- **Issue**: If the bomb NetworkObject is destroyed by external means (e.g., server admin despawn, scene unload), the detonator's linkedBomb reference becomes a destroyed Unity object (not null in C# but null in Unity). Detonator.OnUseStarted checks `linkedBomb == null` which would catch Unity's fake null, so this is handled. However, OnUnequip calls `linkedBomb.ClearDetonatorRef()` which would throw a MissingReferenceException if the bomb was destroyed.
- **Recommendation**: Add a null check in Detonator.OnUnequip: `if (linkedBomb != null) linkedBomb.ClearDetonatorRef();`

---

## Low Issues

### L1: MoveVisualProjectile Drift
- **File**: ProjectileLauncher.cs, lines 131-137
- **Issue**: Visual-only projectiles on remote clients move with constant velocity (no gravity), while the physics projectile on the owning client has gravity. The visual will drift upward relative to the actual trajectory over time.
- **Recommendation**: Add gravity to the visual movement: `velocity += Physics.gravity * Time.deltaTime;`

### L2: Quaternion Compression Edge Case
- **File**: FragmentSnapshot.cs
- **Issue**: If all four quaternion components are very close in magnitude (e.g., ~0.5 each), the "largest" selection is essentially arbitrary and could differ between encode/decode due to floating point. This is handled correctly since the largest is always positive after the sign flip, but half-precision rounding could cause the reconstructed quaternion to have a slightly wrong sign on the missing component.
- **Recommendation**: This is negligible in practice. The error is sub-degree.

### L3: DisableClientPhysics Uses FindObjectsByType
- **File**: DestructionNetworkManager.cs, lines 194-203
- **Issue**: FindObjectsByType<RayfireRigid> at startup iterates all objects. This is a one-time cost but could be slow in large scenes.
- **Recommendation**: Acceptable for initialization. No action needed.

---

## Summary Verdict: **Needs Fixes Before Ship**

### Prioritized Action List
1. **[CRITICAL] Implement late-join state hydration** - Without this, any player joining after the first demolition sees a completely wrong world state.
2. **[CRITICAL] Gate ProjectileLauncher behind debug** - Or implement server-authoritative fire rate and origin validation.
3. **[HIGH] Add rate limiting to UseServerRpc** - Prevents cooldown bypass on all tools.
4. **[HIGH] Add null check in Detonator.OnUnequip** - Prevents MissingReferenceException crash.
5. **[MEDIUM] Batch EnforceClientKinematic** - Run once per frame after all demolitions rather than per-demolition.
6. **[MEDIUM] Verify Bomb prefab has NetworkTransform** - Or add explicit transform sync for thrown bombs.
7. **[MEDIUM] Pool the snapshot array in CaptureHostState** - Reduce GC pressure on hot path.
8. **[LOW] Add gravity to visual projectile movement** - Cosmetic improvement.
