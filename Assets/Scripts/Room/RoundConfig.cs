using System;
using UnityEngine;

// Data that defines a single round within a room.
// Serializable so it can be embedded inline in RoomConfig inspector arrays.
[Serializable]
public class RoundConfig
{
    [Tooltip("Enemies to spawn when this round begins.")]
    public EnemySpawnEntry[] Enemies = Array.Empty<EnemySpawnEntry>();

    [Tooltip("Countdown duration in seconds. " +
             "Set to 0 on the final round — it only ends when all enemies are dead.")]
    [Min(0f)]
    public float TimerDuration = 30f;

    // Convenience: true means this round has no timer (final round behaviour).
    public bool IsTimerless => TimerDuration <= 0f;
}

// One entry in a round — which enemy prefab to spawn and how many.
[Serializable]
public class EnemySpawnEntry
{
    [Tooltip("Enemy prefab to instantiate. Must have Health (and ideally DeathController).")]
    public GameObject EnemyPrefab;

    [Tooltip("Number of this enemy type to spawn in the round.")]
    [Min(1)]
    public int Count = 1;
}
