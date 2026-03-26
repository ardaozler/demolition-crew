---
status: reverse-documented + aligned with Notion GDD v0.01
source: Assets/Scripts/Tools/, Assets/Scripts/Interaction/, Notion GDD
date: 2026-03-26
---

# Tool System Design

> **Note**: This document merges the existing implementation with the Notion GDD.
> Many tools from the GDD are not yet implemented.

## Overview

The tool system provides players with equipment for demolishing structures and
interacting with the environment. Tools are physical objects in the world that
can be picked up, used, and dropped. All tools have **durability** and break
with use. Tools can also be used on teammates (Friendly Fire pillar). All tool
actions are server-authoritative with client input validation.

## Player Fantasy

Grab a sledgehammer and feel the weight of each swing as walls crumble. Watch
the durability tick down — better make each hit count. Need more power? Order
a drill from the Van and bore through reinforced walls. Or just drill your
friend for laughs while they're trying to work.

## Detailed Rules

### Equipment System (EquipmentHandler) — Implemented

Players can hold one tool at a time. Interactions are context-sensitive:

**Priority chain** (checked in order):
1. Targeting grinder + carrying debris → Deposit debris
2. Targeting debris + hands free + not carrying player → Pick up debris
3. Targeting player + hands free → Pick up player
4. Targeting interactable + hands free → Equip tool
5. No target → Drop what you're holding

**Equip mechanics:**
- Range: 4m maximum
- Swap: picking up a new tool drops the current one
- Physics: held items become kinematic, dropped items become dynamic
- Position: held items snap to holdPoint every LateUpdate
- Drop position: 1.5m forward + 0.5m up from player

**Mutual exclusivity:**
- Cannot hold tool + carry debris simultaneously
- Cannot hold tool + carry player simultaneously
- Cannot carry debris + carry player simultaneously

### Rate Limiting

| Action | Cooldown | Purpose |
|--------|----------|---------|
| Equip/swap | 0.2s | Prevent equip spam |
| Tool use | 0.15s | Prevent RPC flood |

### Cheat Prevention

- Aim origin must be within 2m of player position
- Aim direction must be normalized (sqrMagnitude > 0.01)
- Equip range validated server-side (4m max)
- TrySetParent race: if another player grabs first, fail silently

### Tool Durability (Not Implemented — From Notion GDD)

All tools have a durability value that decreases with each use. When durability
reaches 0, the tool breaks and is destroyed. This creates resource pressure and
feeds the economy loop (need to order replacements via air-support).

```
durability -= durabilityLossPerUse
if (durability <= 0) → tool breaks (despawn, play break VFX/SFX)
```

---

## Tool Roster

### Implemented Tools

#### Sledgehammer ✅

**Type**: Melee destruction tool (default starting tool)

**Mechanic**: Raycast from aim point in aim direction. On hit, apply RayFire
damage and physics knockback.

| Parameter | Value | Unit |
|-----------|-------|------|
| Damage | 50 | RayFire HP |
| Hit Range | 3 | meters |
| Damage Radius | 0.5 | meters |
| Cooldown | 0.8 | seconds |
| Hit Force | 10 | Newtons (impulse) |
| Lateral Bias | 0.3 | multiplier |
| Durability | TBD | uses |

**Knockback formula:**
```
right = Cross(Vector3.up, aimDirection).normalized
forceDirection = (aimDirection + right * lateralBias).normalized
force = forceDirection * hitForce
Applied via: AddForceAtPosition(force, hitPoint, ForceMode.Impulse)
```

**Friendly Fire**: When hitting a teammate, sledgehammer sends them flying
(per Notion GDD). Not yet implemented.

#### Bomb ✅

**Type**: Throwable explosive with remote detonation

**Flow:**
1. Player equips bomb
2. Player uses bomb → bomb is thrown, detonator auto-equipped
3. Player uses detonator → bomb explodes, detonator despawns

**Throw mechanics:**
| Parameter | Value | Unit |
|-----------|-------|------|
| Throw Force | 15 | m/s (VelocityChange) |
| Spawn Offset | 0.5 | meters ahead of player |

**Explosion mechanics (RayfireBomb):**
| Parameter | Value | Unit |
|-----------|-------|------|
| Range | 8 | meters |
| Strength | 5 | RayFire force |
| Damage | 100 | RayFire HP |
| Chaos | 30 | fragment scatter randomness |
| Variation | 50 | explosion asymmetry |

**State management:**
- Thrown bomb can be picked back up (resets throw state, destroys detonator)
- Detonator dropped without use → clears bomb reference (bomb stays in world)
- Bomb→Detonator swap uses ServerForceEquip (bypasses normal equip flow)
- One-way detonation flag prevents double-explode

#### Detonator ✅

**Type**: Remote trigger for placed bombs (temporary item)

**Lifecycle:**
- Spawned automatically when bomb is thrown
- Auto-equipped to the bomb thrower via ServerForceEquip
- Despawned after detonation or if bomb is picked back up

**Use action:** Triggers linked bomb's Detonate() method, then self-destructs.

#### RocketLauncher 🔲 (Stub)

**Type**: Ranged explosive projectile

**Status**: Stub class exists, no implementation.

**Design intent**: Long-range structural damage. Projectile explodes on impact
with RayfireBomb-style area destruction.

---

### Planned Tools (From Notion GDD — Not Implemented)

All planned tools have durability loss.

| Tool | Type | Destruction Use | Friendly Fire Use |
|------|------|----------------|-------------------|
| **Pickaxe** | Melee | Break walls, precision damage | Hit teammates |
| **Shovel** | Melee | Dig/scrape, debris clearing | Hit teammates |
| **Screwdriver** | Melee | Disassemble, precision work | Stab teammates |
| **Hammer** | Melee | Light demolition, nailing | Hit teammates |
| **Drill** | Power tool | Bore through walls, reinforced material | Drill teammates |
| **Lighter** | Utility | Ignite flammable materials | Burn teammates |
| **Ladder** | Utility | Reach higher floors/areas | — |
| **Wheelbarrow** | Transport | Carry more debris at once | — |
| **Gloves** | Passive | Improved grip/carry speed | — |

**Design notes:**
- Each tool should have distinct destruction strengths (e.g., drill beats
  reinforced concrete, sledgehammer is general-purpose)
- Ladder and wheelbarrow are utility tools, not damage tools
- Gloves are a passive buff (ordered via air-support)

---

### Vehicle Collision Damage ✅

**Type**: Passive damage system for heavy vehicles

| Parameter | Value | Unit |
|-----------|-------|------|
| Damage Per Hit | 30 | RayFire HP |
| Damage Radius | 0.5 | meters |
| Min Impact Force | 5 | Newtons (threshold) |

**Mechanic**: On collision, if impact impulse exceeds threshold, apply RayFire
damage at contact point. Automatically un-kineticizes shards before damaging.
Host-only (client collisions ignored).

---

### Air-Support Orderable Items (From Notion GDD — Not Implemented)

These items are ordered from the Van and delivered by helicopter:

| Item | Category | Notes |
|------|----------|-------|
| TNTs | Explosive | Placeable explosives |
| Fuel Tanks | Explosive | Environmental explosive |
| Tools (all above) | Equipment | Replacement tools with durability |
| Extra Workers (NPCs) | Support | AI helpers that assist with demolition |
| Med-kits | Health | Heal player damage |
| Excavator | Vehicle | Driveable heavy machinery |
| Wrecking Ball Crane | Vehicle | Driveable crane with swinging ball |
| Air-Strike | Bombardment | Targeted area destruction from above |
| Nuke | Fun element | Maximum destruction (fun/spectacle) |

**Delivery mechanic:** Helicopter drops items at/near Van. Loot drops can
**hurt players** if they land on them (Work Accidents pillar).

## Formulas

### Sledgehammer Knockback
```
right = Cross(Vector3.up, aimDirection).normalized
forceDirection = (aimDirection + right * lateralBias).normalized
force = forceDirection * hitForce
```

### Tool Durability (Proposed)
```
durability -= durabilityLossPerUse
if (durability <= 0) → destroy tool

Example: Sledgehammer with 50 durability, 1 loss per swing = 50 swings
```

## Edge Cases

| Edge Case | Solution |
|-----------|----------|
| Two players grab same tool simultaneously | TrySetParent race — first wins, second fails silently |
| Player disconnects while holding tool | OnNetworkDespawn force-drops at player position |
| Bomb picked up after throw | Reset thrown state, destroy active detonator |
| Detonator dropped without detonating | OnUnequip clears bomb reference |
| Use tool while carrying player | Player throw takes priority over tool use |
| Aim origin spoofed | Server validates origin within 2m of player |
| Tool breaks mid-use | Durability check before action, destroy after if depleted |
| Tool hits teammate | Apply knockback/damage to player (friendly fire) |
| Helicopter drop lands on player | Apply impact damage (Work Accidents) |
| NPC worker hit by player tool | TBD — friendly fire applies to NPCs? |

## Dependencies

- **EquipmentHandler** — Core equip/unequip orchestration
- **InteractionDetector** — SphereCast target detection (0.2m radius, 3m range)
- **InteractionHighlighter** — Fresnel glow + "[E] prompt" hint
- **FragmentCarrier** — Debris pickup/deposit subsystem
- **PlayerCarrier** — Player pickup/throw subsystem
- **RayFire** — Destruction physics (ApplyDamage, RayfireBomb)
- **ScriptableObject settings** — SledgehammerSettings, BombSettings
- **[Needed] DurabilitySystem** — Track and deplete tool durability
- **[Needed] PlayerHealth** — Friendly fire damage application
- **[Needed] AirSupportSystem** — Tool ordering and helicopter delivery
- **[Needed] NPCWorkerAI** — AI helpers ordered via air-support

## Tuning Knobs

| Parameter | Location | Default | Purpose |
|-----------|----------|---------|---------|
| Sledgehammer Damage | SledgehammerSettings | 50 | Destruction per swing |
| Sledgehammer Cooldown | SledgehammerSettings | 0.8s | Swing rate |
| Sledgehammer Range | SledgehammerSettings | 3m | Reach |
| Bomb Throw Force | BombSettings | 15 | Throw distance |
| Bomb Range | BombSettings | 8m | Explosion radius |
| Bomb Damage | BombSettings | 100 | Destruction per explosion |
| Max Equip Range | EquipmentHandler | 4m | Pickup distance |
| Interaction Range | InteractionDetector | 3m | Detection distance |
| Player Throw Force | PlayerCarrier | 15 | Yeet distance |
| Max Carry Extent | CarryableDebris | 2m | Max debris size |
| [Needed] Tool durability | Per-tool settings | TBD | Uses before breaking |
| [Needed] Friendly fire damage | Per-tool settings | TBD | Damage to teammates |

## Acceptance Criteria

- [x] Player can equip, use, and drop all implemented tools
- [x] Sledgehammer destroys RayFire objects within range
- [x] Bomb throw → detonator equip → detonate flow works end-to-end
- [x] Picking up a new tool drops the current one
- [x] Remote players see correct tool in hand (NetworkVariable sync)
- [x] Disconnecting player's tool is dropped, not orphaned
- [x] All tool use is server-authoritative (no client-side destruction)
- [ ] Tool durability depletes with use and tool breaks at 0
- [ ] Sledgehammer sends teammates flying on hit (friendly fire)
- [ ] At least 3 additional tools from the Notion GDD roster implemented
- [ ] Drill can bore through reinforced materials
- [ ] Ladder allows reaching higher floors
- [ ] Wheelbarrow allows carrying more debris
- [ ] All tools orderable via air-support system
