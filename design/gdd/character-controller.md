---
status: reverse-documented + aligned with Notion GDD v0.01
source: Assets/Scripts/CharacterController/, Notion GDD
date: 2026-03-26
---

# Character Controller Design

> **Note**: This document merges the existing implementation with the Notion GDD.
> Player health, flashlight, and slow move are not yet implemented.

## Overview

First-person character controller built on a modular handler architecture. Each
movement system (horizontal movement, gravity, jumping) is a separate component
with ScriptableObject-driven tuning. The controller prioritizes snappy, responsive
movement appropriate for a chaotic co-op demolition game. Players can be hurt by
tools, explosions, and falling debris (Work Accidents / Friendly Fire pillars).

## Player Fantasy

Movement feels tight and responsive — you can quickly navigate rubble-strewn
demolition sites, hop over debris, and precisely position yourself for tool use.
Toggle the flashlight to explore dark interiors. Walk carefully on unstable floors.
Watch out for falling walls and your "helpful" teammates with sledgehammers.

## Detailed Rules

### Architecture

```
Player GameObject
├── CharacterMotor        (Rigidbody wrapper, velocity API)
├── InputProvider          (Input System polling + events)
├── MovementHandler        (horizontal locomotion)
├── GravityHandler         (vertical acceleration + fall speed)
├── JumpHandler            (jump state machine + buffering)
├── GroundDetector         (sphere/raycast ground check)
├── FirstPersonCamera      (pitch/yaw, cursor lock)
├── CameraInputProvider    (look input)
├── NetworkPlayerSetup     (enable/disable for owner vs remote)
├── [Needed] PlayerHealth  (HP, damage, death, respawn)
├── [Needed] Flashlight    (toggleable light, F key)
└── [Needed] SlowMove      (walk mode, Shift key)
```

All handlers use `GetComponent<>()` in Awake for dependency resolution.
Physics code runs in FixedUpdate; input polling in Update.

### Controls (From Notion GDD)

| Input | Action | Status |
|-------|--------|--------|
| **WASD** | Move | ✅ Implemented |
| **E** | Interact | ✅ Implemented |
| **F** | Flashlight | 🔲 Not Implemented |
| **Shift** | Slow Move | 🔲 Not Implemented |
| **Space** | Jump | ✅ Implemented |
| **Mouse X/Y** | Look Around | ✅ Implemented |
| **LMB** | Hit & Use tool | ✅ Implemented |

### Horizontal Movement

**Speed calculation:**
```
targetSpeed = inputMagnitude * moveSpeed    (capped at maxSpeed)
acceleration = isGrounded ? acceleration : acceleration * airControlMultiplier
velocityDelta = targetVelocity - currentHorizontalVelocity
correction = ClampMagnitude(velocityDelta, accel * deltaTime)
Apply via ForceMode.VelocityChange
```

| Parameter | Value | Unit |
|-----------|-------|------|
| Move Speed | 8 | m/s |
| Max Speed | 15 | m/s |
| Acceleration | 50 | m/s² |
| Deceleration | 40 | m/s² |
| Air Control | 0.5× | multiplier |
| [Needed] Slow Walk Speed | TBD | m/s (Shift key) |

**Time to max speed:** 0.3s (ground), 0.6s (air)

### Gravity

Three-state multiplier system on top of Physics.gravity (-9.8 m/s²):

| State | Condition | Multiplier | Effective Gravity |
|-------|-----------|------------|-------------------|
| Ascending (jump held) | velocity.y > 0 && jumpHeld | 1.0× | -19.6 m/s² (2× base) |
| Early release | velocity.y > 0 && !jumpHeld | 2.0× | -39.2 m/s² (4× base) |
| Falling | velocity.y < 0 | 2.5× | -49.0 m/s² (5× base) |

**Terminal velocity:** 30 m/s (hard cap)

### Jump

| Parameter | Value | Unit |
|-----------|-------|------|
| Jump Force | 10 | m/s (upward velocity) |
| Coyote Time | 0.15 | seconds |
| Jump Buffer | 0.1 | seconds |
| Jump Cut Multiplier | 0.5 | (halves upward velocity on early release) |
| Min Jump Time | 0.1 | seconds (before cut can activate) |
| Max Jumps | 1 | (double jump stored but not implemented) |

**Jump arc (full hold):**
```
Time to peak: 10 / 49 ≈ 0.2 seconds
Max height: 10² / (2 × 49) ≈ 1.0 units
Total airtime: ~0.4 seconds
```

### Ground Detection

| Parameter | Value |
|-----------|-------|
| Method | SphereCast (default) or Raycast |
| Radius | 0.3 units |
| Offset | (0, -0.95, 0) from transform |
| Layer Mask | Configurable |

### Camera

| Parameter | Value |
|-----------|-------|
| Horizontal Sensitivity | 2.0 |
| Vertical Sensitivity | 2.0 |
| Pitch Clamp | ±89° |
| Cursor | Locked + hidden |

**Separation:** Yaw rotates player body (world Y). Pitch rotates camera only (local X).

### Network Integration

**Owner (local player):**
- All handlers enabled
- Rigidbody interpolation = Interpolate (smooth local movement)

**Remote players:**
- All handlers disabled (InputProvider, Movement, Jump, Gravity, GroundDetector)
- Camera + AudioListener disabled
- InteractionDetector + Highlighter disabled
- Position driven by NetworkTransform or custom sync

### Player Carry & Throw (Friendly Fire Pillar)

| Parameter | Value |
|-----------|-------|
| Pickup Range | 3m |
| Throw Force | 15 (ForceMode.Impulse) |
| Carry Rotation | Y-axis only (match carrier facing) |

**Rules:**
- Can carry one player at a time
- Cannot carry while holding tool or debris
- Cannot carry while being carried
- Carried player becomes kinematic, NetworkTransform disabled
- Throw applies force on the carried player's owner client
- Disconnect while carrying/being carried: physics restored, references cleared

**Friendly Fire scenarios (from Notion GDD):**
- Grab and hold teammate (game interruption / trolling)
- Throw teammate under falling building parts
- Throw teammate near explosions
- Throw into water, pools, scientific pools, water tanks

### Player Health System (Not Implemented — From Notion GDD)

**Work Accidents pillar** requires players to take damage from:
- Explosions (bombs, TNT, environmental)
- Falling debris, pillars, walls
- Teammate tools (friendly fire)
- Vehicle collisions
- Helicopter loot drops landing on player

**Friendly Fire pillar** requires:
- Sledgehammer sends hit players flying
- Drill damages teammate
- Screwdriver stab damage
- Lighter burn damage

**Design questions (TBD):**
- How much HP? Respawn mechanic?
- Downed state or instant death?
- Med-kits from air-support to heal?
- Fall damage from being thrown or from heights?

### Flashlight (Not Implemented — From Notion GDD)

- Toggle with **F** key
- Directional light attached to camera
- Useful for exploring dark building interiors
- Network synced (other players see your flashlight)

### Slow Move (Not Implemented — From Notion GDD)

- Hold **Shift** to walk slowly
- Useful for careful navigation on unstable floors/debris
- Reduced speed (proposed: 3-4 m/s vs 8 m/s normal)

## Formulas

### Movement Acceleration
```
correction = Clamp(targetVelocity - currentVelocity, accel * Δt)

Ground:  8 m/s ÷ 50 m/s² = 0.16s to reach walk speed
         15 m/s ÷ 50 m/s² = 0.30s to reach max speed
Air:     15 m/s ÷ 25 m/s² = 0.60s to reach max speed
```

### Gravity Formula
```
extraGravity = Physics.gravity * (gravityScale * multiplier - 1)
totalGravity = Physics.gravity + extraGravity
             = Physics.gravity * gravityScale * multiplier
```

### Jump Height
```
maxHeight = jumpForce² / (2 * gravityScale * fallMultiplier * |Physics.gravity|)
          = 100 / (2 * 2 * 2.5 * 9.8)
          = 100 / 98 ≈ 1.02 meters
```

## Edge Cases

| Edge Case | Solution |
|-----------|----------|
| Diagonal input > 1.0 magnitude | Normalized in InputProvider |
| Pitch wraparound (270° → -90°) | Corrected on Start: if > 180, subtract 360 |
| Jump during coyote time | lastGroundedTime tracked, allows 150ms grace |
| Jump input before landing | Buffer timer queues input for 100ms |
| Accidental jump cut (brief release) | minJumpTime (0.1s) prevents early cuts |
| Mid-air re-jump | lastGroundedTime set to -999 after jump |
| Camera action map missing | Falls back from "Camera" to "Player" map |
| Player killed while carrying teammate | Drop carried player, restore physics |
| Player thrown into explosion | Take explosion damage + knockback |
| Player hit by falling debris | Take damage proportional to impact |
| Flashlight toggled during carry | Should still work while carrying |

## Dependencies

- **Unity Input System** 1.18 — InputActionAsset for Move, Jump, Use, Interact, Look
- **Unity Physics** (PhysX) — Rigidbody-based movement
- **Netcode for GameObjects** — NetworkPlayerSetup for owner/remote handling
- **ScriptableObjects** — MovementSettings, JumpSettings, GravitySettings, CameraSettings
- **[Needed] PlayerHealth** — HP, damage, death/respawn system
- **[Needed] FlashlightController** — Toggle light, network sync
- **[Needed] SlowMoveHandler** — Shift-to-walk mechanic

## Tuning Knobs

| Parameter | ScriptableObject | Default |
|-----------|------------------|---------|
| moveSpeed | MovementSettings | 8 m/s |
| maxSpeed | MovementSettings | 15 m/s |
| acceleration | MovementSettings | 50 m/s² |
| deceleration | MovementSettings | 40 m/s² |
| airControlMultiplier | MovementSettings | 0.5 |
| jumpForce | JumpSettings | 10 m/s |
| coyoteTime | JumpSettings | 0.15s |
| jumpBufferTime | JumpSettings | 0.1s |
| jumpCutMultiplier | JumpSettings | 0.5 |
| gravityScale | GravitySettings | 2.0 |
| fallMultiplier | GravitySettings | 2.5 |
| maxFallSpeed | GravitySettings | 30 m/s |
| horizontalSensitivity | CameraSettings | 2.0 |
| verticalSensitivity | CameraSettings | 2.0 |
| [Needed] slowWalkSpeed | MovementSettings | TBD |
| [Needed] maxHP | PlayerHealthSettings | TBD |
| [Needed] flashlightRange | FlashlightSettings | TBD |

## Acceptance Criteria

- [x] WASD movement with acceleration/deceleration
- [x] Variable jump height (hold longer = jump higher)
- [x] Coyote time and jump buffering feel responsive
- [x] Snappy fall speed (no floaty jumps)
- [x] Air control at reduced rate
- [x] First-person camera with pitch clamping
- [x] Remote players: no local physics or input
- [x] Player carry and throw working in multiplayer
- [ ] Flashlight toggleable with F key, visible to other players
- [ ] Slow walk with Shift key
- [ ] Player takes damage from explosions, debris, tools
- [ ] Sledgehammer sends hit players flying
- [ ] Player death/down state and respawn mechanic
- [ ] Med-kit healing from air-support
- [ ] Sensitivity settings exposed in options menu
- [ ] Controller support (TBD)
