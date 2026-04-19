# Item System - Implementation Plan

## Overview
Persistent equipment system that survives across runs. Starting with weapon slot + gold currency.
Designed to extend later: rarity tiers, elements, more slots, item progression, shops.

## Design Decisions
- Scope: Persistent equipment (across runs)
- Rarity: Designed for, not implemented yet
- Obtaining: Multiple sources (drops, chests, shops, rewards)
- Elements: Designed for, not implemented yet
- Slots: Weapon only for now
- Item progression: No leveling/fusing yet
- Currency: Gold system built with items
- Weapon mechanics: Flexible (stat mods, attack swap, or both)

## Architecture

### Data Layer
- ItemDefinition (ScriptableObject) - defines an item with:
  - itemId (string key for save/load)
  - name, description, icon
  - slot (EquipmentSlot enum)
  - rarity (field exists, no effect yet)
  - statModifiers[] (array of stat bonuses)
  - attackOverride (optional AttackDefinition - swaps player attack type)
  - elementAffinity (field exists, no effect yet)

- ItemRegistry (ScriptableObject) - maps itemId to SO, index-based lookup for networking

### Stat Recomputation
Items can be unequipped, but existing stat APIs are add-only.
Solution: On equip/unequip, recompute ALL stats from base values + upgrades + equipment.

### Networking
- PlayerEquipment = NetworkBehaviour (stats affect combat, visible to all)
- PlayerCurrency = NetworkBehaviour (server-authoritative gold)
- PlayerInventory = MonoBehaviour (private, local only)
- GoldDropper = MonoBehaviour (server-only, hooks enemy death)

### Persistence
- SaveData class (gold, item IDs, equipped weapon)
- SaveManager singleton (PlayerPrefs + JsonUtility)
- Load on session start, save on session end

---

## Concepts

### ItemDefinition
A ScriptableObject — a data asset that describes one item. Think of it like a stat card for a weapon:

```
Iron Sword
  slot: Weapon
  rarity: Common
  statModifiers: [DamagePercent = 0.15]   ← +15% damage while equipped
  attackOverride: SwordAttack             ← swaps player's projectile to a melee swing
```

Create one asset per item in the game. Holds no runtime state — just the definition.
Same pattern as UpgradeDefinition.

### ItemRegistry
A lookup table — an ordered list of all ItemDefinition assets in one place.

Exists for two reasons:
1. Save/Load — store the item's string itemId on save. On load, ItemRegistry.GetById("iron_sword") returns the asset.
2. Networking — can't send a ScriptableObject over the network. Send its index (int) instead. The other client calls ItemRegistry.GetByIndex(2) and gets the same item.

### How to use in Unity Editor
1. Right-click in Project → Create → Arcon/Item/Item Definition → fill in name, slot, stat modifiers
2. Right-click → Create → Arcon/Item/Item Registry → drag all item assets into the items array

Session 2 (PlayerInventory + PlayerEquipment) is where these get used in gameplay — equipping an item reads its ItemDefinition and applies the stats.

### Gold System (Session 3)
Three components work together:

**LootTable** (ScriptableObject)
- Defines how much gold an enemy drops: goldMin / goldMax range
- Create: Right-click in Project → Create → Arcon/Item/Loot Table
- One asset can be shared across many enemies of the same type (e.g. all goblins share GoblinLoot)

**GoldDropper** (MonoBehaviour — add to enemy prefabs)
- Hooks into Health.OnDeath automatically
- Rolls gold from the assigned LootTable and credits the killing player
- Setup: Add GoldDropper component to enemy prefab → assign a LootTable asset in the inspector

**PlayerCurrency** (NetworkBehaviour — add to player prefab)
- Holds the player's gold count, synced to all clients via NetworkVariable
- Add PlayerCurrency to the Player prefab alongside PlayerInventory and PlayerEquipment

**Gold flow:**
1. Player kills enemy → Health.OnDeath fires
2. GoldDropper reads DamageInfo.Source (the player GameObject)
3. Finds PlayerCurrency on the source, calls AddGold()
4. Gold count syncs automatically to all clients via NetworkVariable

---

## Work Sessions

### Session 1: Data Layer + Effect System ✅
Files created:
- Assets/Scripts/Item/ItemEnums.cs (EquipmentSlot, ItemEffectType, ItemRarity)
- Assets/Scripts/Item/ItemStatModifier.cs (serializable struct)
- Assets/Scripts/Item/ItemDefinition.cs (ScriptableObject)
- Assets/Scripts/Item/ItemRegistry.cs (ScriptableObject with ID/index lookup)
- Assets/Scripts/Item/ItemEffectRegistry.cs (handler registry, mirrors UpgradeEffectRegistry)

Also modified:
- UpgradeEffectRegistry.cs — added CritHandler field to UpgradeContext for item crit effects

Integration: Mirrors existing UpgradeEffectRegistry pattern
Verify: Project compiles, can create ItemDefinition assets in Unity editor ✅

### Session 2: Inventory + Equipment
Files to create:
- Assets/Scripts/Item/PlayerInventory.cs (MonoBehaviour, local storage)
- Assets/Scripts/Item/PlayerEquipment.cs (NetworkBehaviour, equip/unequip + stats)

Files to modify:
- PlayerUpgradeHandler.cs - expose _acquiredUpgrades for stat recomputation
- PlayerMovement.cs - add ResetSpeed() for recomputation
- PlayerCritHandler.cs - add ResetCritStats() for recomputation

Integration:
- PlayerEquipment calls AttackController.SetAttackDefinition() for weapon swap
- PlayerEquipment calls ItemEffectRegistry for stat application
- Recomputation rebuilds stats from base + upgrades + equipment

Verify: Equip/unequip weapon via code, stats change correctly (check logs)

### Session 3: Currency + Gold Drops
Files to create:
- Assets/Scripts/Item/PlayerCurrency.cs (NetworkBehaviour, NetworkVariable<int>)
- Assets/Scripts/Item/GoldDropper.cs (hooks Health.OnDeath on enemies)
- Assets/Scripts/Item/LootTable.cs (ScriptableObject, extension point)

Integration:
- GoldDropper subscribes to Health.OnDeath
- DamageInfo.Source used for kill attribution
- GoldDropper credits killer's PlayerCurrency

Verify: Kill enemies, see gold increase in logs

### Session 4: Save/Load + Wiring
Files to create:
- Assets/Scripts/Save/SaveData.cs (serializable class)
- Assets/Scripts/Save/SaveManager.cs (singleton, PlayerPrefs)

Wiring:
- Add PlayerInventory, PlayerEquipment, PlayerCurrency to Player prefab
- Add GoldDropper to enemy prefabs
- Create ItemRegistry asset + 2-3 example weapons
- Wire save/load into game lifecycle

Verify: Play, earn gold, equip weapon, close game, reopen - state persists

### Session 5: UI (separate scope)
- Gold display on HUD
- Basic equipment/inventory screen
- Equip/unequip buttons

Verify: Full flow works through UI

---

## Key Integration Points
- AttackController.SetAttackDefinition() - weapon attack swap
- AttackController.SetDamageMultiplier() - damage% items
- AttackController.SetCooldownMultiplier() - attack speed% items
- Health.AdjustMaxHealth() - HP items
- Health.OnDeath - gold drops on enemy death
- DamageInfo.Source - kill attribution for gold
- PlayerMovement.AddSpeedBonus/Multiplier() - speed items
- PlayerCritHandler.AddCritChance() - crit items
- UpgradeContext class - reused for item effect application

## Extension Points (future)
- Rarity tiers (ItemRarity enum ready)
- Element weapons (elementAffinity field ready)
- More slots (EquipmentSlot enum, dictionary supports it)
- Item leveling/fusing
- Item drops from enemies (LootTable SO ready)
- Shop system
