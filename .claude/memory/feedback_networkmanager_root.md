---
name: NetworkManager must be at scene root
description: Unity NGO's NetworkManager cannot be nested under other GameObjects
type: feedback
---

Never place NetworkManager as a child of another GameObject (e.g. ── Managers ──).

**Why:** Unity's NGO enforces that NetworkManager lives at the scene root. Nesting it causes a warning/error and Unity auto-fixes it by moving it to the root anyway.

**How to apply:** Always create the NetworkManager at the scene root, not under any parent container.
