Spawns a batch of minions on the NavMesh. CD starts only after the last minion dies. One batch at a time.

Use MinionSummoner component on the enemy prefab. Set minionPrefab, minionsPerWave, summonInterval, spawnRadius, and spawnInFront in the Inspector.

## Multi-Wave
Add multiple MinionSummoner components to the same enemy — each runs independently with its own batch, CD, and minion type. For example: one summoner spawns goblins every 5s, another spawns skeletons every 10s.
