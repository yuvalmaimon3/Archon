---
name: Element System Design
description: Planned elemental reaction system inspired by Genshin Impact — two elements combining creates a reaction with extra damage
type: project
---

The game will have an element system inspired by Genshin Impact.

**Core mechanic:** When two elements interact (e.g., applied to the same target), they trigger a reaction that deals bonus/extra damage.

**Why:** Part of the core combat design to add depth and strategic choices to the roguelite attack loop.

**How to apply:** When designing the attack system, account for element tagging on hits, element storage on targets, and reaction calculation logic. Keep elements data-driven (ScriptableObjects) so new elements and reactions can be added easily.
