---
status: reverse-documented + aligned with Notion GDD v0.01
source: Full codebase analysis + Notion GDD (Seçkin Ege Aydemir & Arda Özler)
date: 2026-03-26
---

# Destruction Crew — Game Concept

> **Design:** Seçkin Ege Aydemir & Arda Özler
> **Idea Generation:** Arda Özler
> **Documentation:** Seçkin Ege Aydemir

## Overview

Destruction Crew is a cooperative first-person demolition game where 2-4 players
work together as a demolition crew. Players arrive at job sites in a van, explore
the structure, decide on their approach, tear down buildings using tools and
vehicles, collect loot and debris for money, and race against the clock to meet
demolition targets. The tone is chaotic, sloppy, and funny — friendly fire is a
feature, not a bug.

**Genre:** Destruction / First-Person / Friend Slop

**Target Audience:** Fans of RV There Yet?, PEAK

**The Hook:** Destroy an entire building as much as you can with your teammates
in a limited time and with a limited number of tools.

## Player Fantasy

You and your friends are a ragtag demolition crew taking on increasingly complex
jobs. Explore the building, plan your approach, smash walls with sledgehammers,
plant bombs on load-bearing columns, drive an excavator through the front door —
and throw your buddy into the blast zone for laughs. Call in air support when you
need the big guns. Try not to get crushed by falling debris.

## Game Pillars

1. **Decision Making** — Explore the destruction site. Decide which materials and
   tools are needed. Buy them and start destruction. Strategic planning is rewarded.

2. **Physics** — Use physics to make the best decisions. Destroy the building
   efficiently using less materials. Chain reactions, structural collapses, and
   clever tool placement matter.

3. **Friendly Fire** — Teammates can hold and throw each other to troll or save
   them. Tools hurt friends. Sledgehammers send players flying. Drills, screwdrivers,
   lighters — everything is a weapon against your friends.

4. **Dynamic Environment Objects** — Players can interact with and use environmental
   objects to increase destruction effects:
   - Boiler Rooms (explosive potential)
   - Mechanical Rooms (machinery interactions)
   - Electric Cables (hazards)
   - Electric Rooms (power systems)

5. **Work Accidents** — Stay away from explosions, falling pillars, and collapsing
   walls. The destruction site is dangerous. Players can be hurt or killed by the
   environment and each other.

6. **Air-Support** — Players earn currency in real-time while destroying. Order
   support from the Van — a helicopter delivers tools, vehicles, TNT, NPC workers,
   or even an air-strike. Loot drops can hurt players if they land on them.

## Core Game Loop

```
Menu → Create/Join Lobby (Steam direct connect)
  → Start Game
    → Spawn in Van (cinematic drive to site)
      → Step 1: EXPLORE — Explore the destruction site
        → Step 2: DECIDE — Decide which materials you need
          → Step 3: DESTROY & EARN — Start destroying with your tools
            → Step 4: SUPPORT — Call additional materials/tools with earnings
              → Step 5: REACH THE LIMIT — Hit minimum demolition threshold
                → Step 6: STAY OR LEAVE — Stay for more currency or leave
                  → Step 7: RETURN — All teammates return to Van → next site
```

## Session Flow

### 1. Lobby
- Direct connection via Steam API (no matchmaking server)
- Host creates session, friends join via Steam invite or IP
- Host selects the demolition job/stage

### 2. Van Ride (Transition)
- Players spawn inside a van
- Short cinematic drive to the demolition site
- Sets the tone, builds anticipation
- Players exit van on arrival
- Van serves as the **ordering point** for air-support during the stage

### 3. Exploration Phase
- Timer starts on arrival
- Players explore the building interior
- Find lootable leftover items (currency source)
- Identify structural weak points, environmental hazards
- Locate dynamic environment objects (boilers, electrical rooms, etc.)

### 4. Demolition Phase
- Start destroying with default tools (Sledgehammer always available)
- Tools have **durability** — they break with use
- Earn currency in real-time from destruction
- Find lootable items inside the building for bonus currency

### 5. Air-Support / Ordering
- Order support **from the Van** (entry point of map)
- A helicopter delivers the requested items
- Available orders: tools, TNT, vehicles, fuel tanks, NPC workers,
  med-kits, gloves, air-strikes, and more
- Loot drops can **hurt players** if they land on them

### 6. Stay or Leave (Risk/Reward)
- After hitting the minimum demolition threshold: **stage is passable**
- Players can **stay** to earn more currency (risk: timer, danger)
- Players can **leave** to bank their earnings safely
- **All teammates must return to the Van** to end the stage

### 7. Win/Lose Condition
- **Pass**: Meet minimum demolition threshold before timer expires,
  all players return to Van
- **Fail**: Timer runs out without meeting threshold
- Bonus rewards for exceeding the minimum threshold

## Controls

| Input | Action |
|-------|--------|
| **WASD** | Move |
| **E** | Interact |
| **F** | Flashlight |
| **Shift** | Slow Move |
| **Space** | Jump |
| **Mouse X/Y** | Look Around |
| **LMB** | Hit & Use tool |

Controller support: TBD

## Art Style

**Semi-realism, Low-Poly** — Construction site aesthetic using PolygonConstruction
asset pack as base. Readable, not photorealistic.

## Multiplayer Architecture

- **Networking**: Unity Netcode for GameObjects (NGO) 2.10
- **Connection**: Direct P2P via Steam API (no relay/matchmaking)
- **Authority**: Host-authoritative (all physics on host, clients are kinematic)
- **Tick Rate**: 30 Hz
- **Transport**: Unity Transport (UDP)

## Target Platform

- PC (Steam)

## Localization

- EFIGS-T: English, French, Italian, German, Spanish, Turkish

## Steam Features (Planned)

- Achievements: TBD
- Cloud Saves: TBD
- Steam Workshop: TBD
- Trading Cards: TBD

## Current Implementation Status

| System | Status | Notes |
|--------|--------|-------|
| Character Controller | ✅ Implemented | Modular, ScriptableObject-driven |
| Destruction Networking | ✅ Implemented | Host-authoritative, delta-compressed |
| Sledgehammer | ✅ Implemented | Melee destruction tool |
| Bomb + Detonator | ✅ Implemented | Throw → remote detonate flow |
| Debris Collection | ✅ Implemented | FragmentCarrier + CarryableDebris |
| Debris Grinder | ✅ Implemented | Deposit point (DebrisGrinder) |
| Player Carry/Throw | ✅ Implemented | Social mechanic |
| Vehicle Collision | ✅ Partial | VehicleCollisionDamage exists, no driveable vehicles |
| RocketLauncher | 🔲 Stub | Class exists, no implementation |
| Tool Durability | 🔲 Not Started | Per Notion GDD: all tools have durability loss |
| Player Health/Damage | 🔲 Not Started | Work Accidents + Friendly Fire pillars |
| Additional Tools | 🔲 Not Started | Pickaxe, Shovel, Screwdriver, Hammer, Drill, Ladder, Wheelbarrow |
| Dynamic Environment | 🔲 Not Started | Boiler, Mechanical, Electrical rooms |
| Air-Support System | 🔲 Not Started | Van ordering + helicopter delivery |
| NPC Workers | 🔲 Not Started | Orderable via air-support |
| Lootable Items | 🔲 Not Started | Currency from exploring building |
| Economy/Currency | 🔲 Not Started | No money tracking |
| Flashlight | 🔲 Not Started | F key mapped but not implemented |
| Slow Move | 🔲 Not Started | Shift mapped but not implemented |
| Van Ride | 🔲 Not Started | No cinematic/transition system |
| Stage Timer | 🔲 Not Started | No timer or win/lose condition |
| Stay-or-Leave | 🔲 Not Started | Risk/reward end-of-stage mechanic |
| Return to Van | 🔲 Not Started | Stage completion trigger |
| Lobby (Steam) | 🔲 Not Started | Currently direct IP only |
| Stage Selection | 🔲 Not Started | Single scene only |
| Driveable Vehicles | 🔲 Not Started | Excavator, Wrecking Ball planned |
| Med-kits | 🔲 Not Started | Requires health system |
| Air-Strike / Nuke | 🔲 Not Started | Fun element from air-support |

## Dependencies

- **RayFire** — Physics-based destruction engine
- **PolygonConstruction** — Art assets (construction site aesthetic)
- **Unity Netcode for GameObjects** — Multiplayer networking
- **Steam API** — Lobby/connection (planned)
