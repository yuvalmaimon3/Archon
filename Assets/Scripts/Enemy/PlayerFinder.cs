using UnityEngine;

// Shared utility for finding the nearest live player.
// Centralizes the FindGameObjectsWithTag + nearest-distance logic that was
// duplicated across EnemyMovementBase, EnemyCombatBrain, and SkeletonArcherBrain.
//
// Dead players are excluded automatically — DeathController changes their tag
// to "Untagged" on death, so FindGameObjectsWithTag never returns them.
//
// Note: FindGameObjectsWithTag allocates each call. Acceptable for current scale;
// production builds should replace with a centrally-managed player registry.
public static class PlayerFinder
{
    // Default tag used to locate player GameObjects.
    private const string DefaultPlayerTag = "Player";

    // Returns the Transform of the nearest live player to the given position.
    // Returns null when no players exist (pre-spawn, all dead, etc.).
    public static Transform FindNearest(Vector3 fromPosition, string playerTag = DefaultPlayerTag)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        if (players.Length == 0) return null;

        Transform nearest       = null;
        float     nearestSqDist = float.MaxValue;

        foreach (var p in players)
        {
            float sqDist = (p.transform.position - fromPosition).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest       = p.transform;
            }
        }

        return nearest;
    }
}
