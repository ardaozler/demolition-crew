---
status: reverse-documented + aligned with Notion GDD v0.01
source: Assets/Scripts/Tools/VehicleCollisionDamage.cs, Notion GDD
date: 2026-03-26
---

# Vehicle System Design

> **Note**: This document merges partial implementation with the Notion GDD.
> Only passive collision damage exists. Driveable vehicles are planned.
> Vehicles are ordered via the air-support system from the Van.

## Overview

The vehicle system provides heavy machinery for large-scale demolition. Players
order vehicles via air-support (delivered by helicopter) and drive them into or
through structures. Vehicles complement hand tools by enabling demolition at a
scale and speed impossible on foot. Vehicles can also hurt teammates and players
caught in their path (Work Accidents / Friendly Fire pillars).

## Player Fantasy

Save up enough from demolition work, order the excavator from the Van. A
helicopter delivers it. Climb in, drive it straight through the front wall, and
watch the whole facade crumble. Or swing a wrecking ball into the side of a
building and see an entire floor give way. Just don't run over your teammates.
Well, maybe just once.

## Detailed Rules

### Vehicle Collision Damage (Implemented)

When a vehicle collides with a destructible object:

1. Check impact impulse > minImpactForce threshold (5N)
2. Find RayfireRigid on the collided object
3. Un-kinematicize the shard if it was kinematic
4. Apply RayFire damage at contact point

| Parameter | Value | Unit |
|-----------|-------|------|
| Damage Per Hit | 30 | RayFire HP |
| Damage Radius | 0.5 | meters |
| Min Impact Force | 5 | Newtons |

**Host-only**: Client collisions are ignored (server authority).

### Planned Vehicles (From Notion GDD)

#### Excavator
- **Type**: Driveable heavy machinery
- **Ordered via**: Air-support from Van
- **Mechanic**: Direct collision damage + arm/bucket manipulation
- **Strengths**: Precise demolition, can reach upper floors with arm
- **Weaknesses**: Slow, expensive ($1000 proposed)
- **Delivery**: Helicopter drop near Van

#### Wrecking Ball Crane
- **Type**: Driveable crane with swinging ball
- **Ordered via**: Air-support from Van
- **Mechanic**: Momentum-based collision damage from swinging ball
- **Strengths**: Massive area destruction, spectacular physics
- **Weaknesses**: Hard to control, most expensive ($1500 proposed)
- **Delivery**: Helicopter drop near Van

### Vehicle Requirements (Not Implemented)

| Feature | Status | Notes |
|---------|--------|-------|
| Vehicle physics (driving) | 🔲 | Wheel colliders or custom |
| Enter/exit vehicle | 🔲 | Player mounting system |
| Vehicle camera | 🔲 | Third-person or cabin view |
| Vehicle ordering | 🔲 | Via air-support system from Van |
| Helicopter delivery | 🔲 | Drop animation near Van |
| Vehicle spawn point | 🔲 | Near Van after delivery |
| Network sync | 🔲 | Vehicle position/state replication |
| Player damage from vehicles | 🔲 | Work Accidents / Friendly Fire |
| Collision damage | ✅ | VehicleCollisionDamage component |

### Vehicle + Player Interactions (From Notion GDD Pillars)

**Work Accidents:**
- Players hit by a moving vehicle take damage
- Players can be crushed between vehicle and structure

**Friendly Fire:**
- Intentionally driving into teammates for laughs
- Vehicle collision sends hit players flying

## Formulas

### Collision Damage (Current)
```
if (collision.impulse.magnitude >= minImpactForce)
    rigid.ApplyDamage(damagePerHit, contactPoint, damageRadius)
```

### Proposed: Mass-Based Damage Scaling
```
impactDamage = baseDamage * clamp(impactForce / referenceForce, 1.0, maxMultiplier)

Example:
  Excavator (mass=5000kg) at 5 m/s hits wall:
  impulse ≈ 25000 N, referenceForce = 100 N, maxMultiplier = 10
  damage = 30 * clamp(250, 1, 10) = 30 * 10 = 300
```

### Proposed: Player Damage from Vehicle
```
playerDamage = vehicleMass * impactSpeed * playerDamageCoefficient
knockbackForce = impactDirection * vehicleMass * knockbackCoefficient
```

## Edge Cases

| Edge Case | Solution |
|-----------|----------|
| Low-speed bump | minImpactForce threshold (5N) filters trivial contacts |
| Kinematic shard hit by vehicle | Un-kinematicize before applying damage |
| Client-side collision | Ignored (host-only check) |
| Vehicle hits non-destructible | No RayfireRigid found, no damage applied |
| Player hit by vehicle | Apply damage + knockback (Work Accidents) |
| Teammate intentionally run over | Friendly fire damage + ragdoll/send flying |
| Vehicle flipped over | Reset mechanic or self-right |
| Multiple vehicles collide | Standard PhysX collision resolution |
| Vehicle destroyed by explosion | TBD — can vehicles be damaged? |
| Player exits moving vehicle | Vehicle continues with momentum |
| Helicopter drops vehicle on player | Impact damage (loot drop danger) |
| Vehicle ordered but no currency | Order rejected by air-support system |

## Dependencies

- **VehicleCollisionDamage** — Passive collision damage component
- **RayFire** — Destruction physics
- **[Needed] VehicleController** — Driving physics and input
- **[Needed] VehicleMountSystem** — Enter/exit mechanic
- **[Needed] AirSupportSystem** — Ordering and helicopter delivery
- **[Needed] PlayerHealth** — Vehicle-to-player damage
- **[Needed] VehicleCamera** — Third-person or cabin view when driving

## Tuning Knobs

| Parameter | Current | Purpose |
|-----------|---------|---------|
| damagePerHit | 30 | Destruction per collision |
| damageRadius | 0.5m | Splash area |
| minImpactForce | 5N | Ignore light bumps |
| [Needed] excavatorSpeed | — | Max drive speed |
| [Needed] excavatorMass | — | Physics weight |
| [Needed] excavatorPrice | $1000 | Air-support cost |
| [Needed] wreckingBallSpeed | — | Max drive speed |
| [Needed] wreckingBallMass | — | Physics weight + ball weight |
| [Needed] wreckingBallPrice | $1500 | Air-support cost |
| [Needed] playerDamageCoeff | — | How much vehicles hurt players |

## Acceptance Criteria

- [x] Vehicle collision damages RayFire objects on host
- [x] Kinematic shards activated on collision
- [x] Low-force collisions ignored
- [ ] At least one driveable vehicle (excavator or wrecking ball)
- [ ] Player can enter/exit vehicle
- [ ] Vehicle orderable via air-support from Van
- [ ] Helicopter delivers vehicle with drop animation
- [ ] Vehicle position synced across network
- [ ] Vehicle collision hurts players (Work Accidents)
- [ ] Vehicle destruction feels impactful and satisfying
- [ ] Wrecking ball physics (pendulum swing)
