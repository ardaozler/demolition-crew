---
status: reverse-documented + aligned with Notion GDD v0.01
source: Assets/Scripts/Interaction/, Notion GDD
date: 2026-03-26
---

# Economy System Design

> **Note**: This document merges existing implementation with the Notion GDD.
> Debris collection and deposit exist. Currency, air-support ordering, lootables,
> and stage progression are not yet implemented.

## Overview

The economy system drives mid-round progression through two currency sources:
**real-time earnings from destruction** and **lootable items found inside buildings**.
Players spend currency at the Van to order air-support — tools, vehicles, TNT,
NPC workers, and more — delivered by helicopter. A risk/reward mechanic at stage
end lets players stay for more earnings or leave safely.

## Player Fantasy

Smash walls, watch your earnings tick up. Explore a back room and find a stash
of valuables. Run back to the Van, order TNT and a drill. A helicopter swoops in
and drops your supplies — watch out, the crate almost crushed your teammate.
Timer's running low but you're close to the bonus threshold. Stay or go?

## Detailed Rules

### Currency Sources

#### 1. Real-Time Destruction Earnings (Not Implemented)
Players earn currency as they destroy the building. Currency accumulates based
on the volume and type of material destroyed.

#### 2. Lootable Items (Not Implemented — From Notion GDD)
Travel and find lootable leftover items inside the building. These provide
bonus currency when collected.

- Items spawn in pre-defined locations within building interiors
- Examples: furniture, electronics, scrap metal, valuables
- Encourages exploration before/during demolition
- Some rooms may be harder to reach (need ladder, drill, etc.)

#### 3. Debris Deposit at Grinder (Implemented)
Players can also pick up debris fragments and deposit them at the Grinder for
additional currency.

**Fragment eligibility:**
- Must be a registered fragment in FragmentRegistry (RegistryId >= 0)
- Maximum extent < 2m on any axis (small enough to carry)
- Not already claimed by another player (static HashSet check)
- CarryableDebris component added automatically to eligible fragments

**Pickup flow:**
1. Player looks at debris (InteractionDetector SphereCast, 3m range)
2. Player presses Interact → PickupServerRpc
3. Server validates: not already carrying, not claimed, within range, size OK
4. Server: set kinematic, disable collider, add to claimed set
5. Fragment follows player's holdPoint every LateUpdate

**Deposit flow:**
1. Player looks at DebrisGrinder (IInteractable)
2. Player presses Interact → DepositServerRpc
3. Server: remove from claimed set, destroy fragment on all clients
4. [NOT IMPLEMENTED] Award currency based on fragment value

### Air-Support Ordering (Not Implemented — From Notion GDD)

Players order supplies **from the Van** (entry point of map). This replaces
the "shop UI" concept from earlier documentation.

**Ordering flow:**
1. Player approaches Van
2. Opens order menu (interact with Van)
3. Selects items to order (if they have enough currency)
4. Currency deducted
5. Helicopter arrives and drops items near the Van
6. **Loot drops can hurt players** if they land on them

**Orderable items:**

| Item | Category | Proposed Price | Notes |
|------|----------|---------------|-------|
| Sledgehammer | Tool | Free (default) | Always available at stage start |
| Pickaxe | Tool | $100 | Precision damage |
| Shovel | Tool | $75 | Debris clearing |
| Screwdriver | Tool | $50 | Disassembly |
| Hammer | Tool | $50 | Light work |
| Drill | Tool | $200 | Reinforced materials |
| Ladder | Utility | $150 | Reach higher areas |
| Wheelbarrow | Utility | $150 | Carry more debris |
| Gloves | Passive | $100 | Better grip/speed |
| TNT | Explosive | $250 | Placeable explosive |
| Fuel Tanks | Explosive | $200 | Environmental explosive |
| Bomb | Explosive | $200 | Throwable + detonator |
| RocketLauncher | Weapon | $350 | Ranged destruction |
| Med-kits | Health | $100 | Heal damage |
| Extra Workers (NPCs) | Support | $500 | AI demolition helpers |
| Excavator | Vehicle | $1000 | Driveable heavy machinery |
| Wrecking Ball Crane | Vehicle | $1500 | Driveable crane |
| Air-Strike | Bombardment | $2000 | Targeted area destruction |
| Nuke | Fun element | $5000 | Maximum destruction |

*Prices are proposals and need playtesting to balance against typical
earnings per minute.*

### Stage Completion (Not Implemented)

**Minimum threshold:**
- Each stage has a minimum demolition percentage to pass
- Displayed as progress bar in HUD

**Stay or Leave (Risk/Reward):**
- Once minimum threshold is met, stage is passable
- Players can **stay** to earn more currency (risk: timer still running,
  building is increasingly dangerous with work accidents)
- Players can **leave** by returning to the Van
- **All teammates must return to the Van** to end the stage and proceed

**Win condition:** Meet minimum threshold + all players at Van before timer
**Fail condition:** Timer expires without meeting threshold
**Bonus:** Extra rewards for exceeding minimum threshold

### Destroyable Rooms (From Notion GDD)

Buildings contain special rooms that provide strategic options:
- Rooms may contain lootable items (currency)
- Some rooms are harder to access (locked, upper floors, reinforced)
- Destroying certain rooms may trigger environmental effects

## Formulas

### Destruction Earnings (Proposed — Not Implemented)
```
earningsPerFragment = fragmentVolume * materialMultiplier * stageMultiplier
fragmentVolume = bounds.size.x * bounds.size.y * bounds.size.z
```

| Material | Multiplier | Rationale |
|----------|------------|-----------|
| Concrete | 1.0 | Base material |
| Wood | 0.7 | Common, less valuable |
| Metal | 1.5 | Harder to demolish |
| Glass | 0.5 | Shatters easily |

### Loot Item Value (Proposed — Not Implemented)
```
lootValue = baseItemValue * rarityMultiplier
```

| Rarity | Multiplier | Example |
|--------|------------|---------|
| Common | 1.0 | Scrap wood, broken furniture |
| Uncommon | 2.0 | Copper wiring, metal fixtures |
| Rare | 5.0 | Electronics, valuables |

### Stage Timer (Proposed — Not Implemented)
```
baseTime = 5 minutes (300 seconds)
stageTime = baseTime * stageTimerMultiplier
```

### Demolition Progress (Proposed — Not Implemented)
```
progress = totalDestroyedVolume / totalDestructibleVolume * 100%
passThreshold = 60% (per stage, configurable)
```

## Edge Cases

| Edge Case | Solution |
|-----------|----------|
| Two players race for same debris | Static claimed set prevents double-pickup |
| Player disconnects while carrying | OnNetworkDespawn drops fragment, removes from claimed |
| Fragment destroyed before pickup | CanInteract checks RegistryId >= 0 |
| Grinder targeted without carrying | CanInteract returns false |
| Fragment too large to carry | MaxCarryableExtent (2m) check in PickupServerRpc |
| Late-join: fragments already deposited | Destroyed fragments not in registry, no issue |
| Helicopter drop hits player | Apply impact damage (Work Accidents pillar) |
| All currency spent, no tools left | Default tools (Sledgehammer) always available |
| Player at Van while others still working | Wait for all players to return |
| Timer expires while ordering | Order cancelled, stage fails |
| NPC worker killed by friendly fire/debris | NPC despawns, no refund |
| Lootable item in inaccessible room | Need specific tool (drill, ladder) to reach |

## Dependencies

- **FragmentCarrier** — Debris pickup and carry logic
- **CarryableDebris** — Fragment eligibility marker (IInteractable)
- **DebrisGrinder** — Deposit point (IInteractable)
- **FragmentRegistry** — Fragment ID tracking and ownership
- **[Needed] CurrencyManager** — Track team funds (shared wallet)
- **[Needed] AirSupportUI** — Van ordering interface
- **[Needed] HelicopterDelivery** — Delivery animation and drop system
- **[Needed] LootableItem** — Collectible items in building interiors
- **[Needed] StageManager** — Timer, threshold, win/lose, stay-or-leave
- **[Needed] DemolitionProgress** — Track destruction percentage
- **[Needed] NPCWorkerAI** — AI demolition helpers
- **[Needed] PlayerHealth** — For helicopter drop damage

## Tuning Knobs

| Parameter | Current | Purpose |
|-----------|---------|---------|
| MaxCarryableExtent | 2m | Max debris size for hand-carry |
| Pickup Range | 3m | How close to pick up debris |
| [Needed] Destruction earnings rate | — | Currency per unit volume destroyed |
| [Needed] Loot item values | — | Currency per loot pickup |
| [Needed] Item prices | — | Air-support order costs |
| [Needed] Stage timer | — | Time pressure per stage |
| [Needed] Demolition threshold | — | Minimum % to pass stage |
| [Needed] Bonus multiplier | — | Extra reward for exceeding threshold |
| [Needed] Helicopter delivery time | — | Seconds from order to drop |
| [Needed] Drop damage radius | — | Hurt range from helicopter drops |

## Acceptance Criteria

- [x] Players can pick up debris fragments
- [x] Players can deposit debris at grinder (fragment destroyed)
- [x] Claimed fragments cannot be picked up by two players
- [ ] Currency earned in real-time from destruction
- [ ] Lootable items collectible inside buildings
- [ ] Running total displayed in HUD
- [ ] Air-support orderable from Van interaction
- [ ] Helicopter delivers ordered items with drop animation
- [ ] Helicopter drops can damage players
- [ ] Stage timer visible and enforced
- [ ] Demolition progress bar tracks destruction percentage
- [ ] Stage passable once minimum threshold met
- [ ] Stay-or-leave choice available after threshold
- [ ] All players must return to Van to complete stage
- [ ] Win/lose screen on stage completion
- [ ] NPC workers assist with demolition when ordered
