using System;
using UnityEngine;

// Data that defines a single round within a room.
// Serializable so it can be embedded inline in RoomConfig inspector arrays.
[Serializable]
public class RoundConfig
{
    [Tooltip("Groups of enemies to spawn when this round begins. " +
             "Each group is one enemy type with a count — a round typically has multiple groups.")]
    public SpawnGroup[] SpawnGroups = Array.Empty<SpawnGroup>();

    [Tooltip("Countdown duration in seconds. " +
             "Set to 0 on the final round — it only ends when all enemies are dead.")]
    [Min(0f)]
    public float TimerDuration = 30f;

    // Convenience: true means this round has no timer (final round behaviour).
    public bool IsTimerless => TimerDuration <= 0f;
}

// One spawn group in a round — an enemy prefab to spawn and how many instances of it.
// A round can mix several groups, e.g. 3 Goblins + 2 Archers + 1 Elite.
[Serializable]
public class SpawnGroup
{
    [Tooltip("Enemy prefab to instantiate. Must have Health (and ideally DeathController).")]
    public GameObject EnemyPrefab;

    [Tooltip("How many of this enemy type to spawn in the round.")]
    [Min(1)]
    public int Count = 1;
}
