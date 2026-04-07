using UnityEngine;

// ScriptableObject that defines all configurable parameters for room generation.
// Create via: Assets > Create > Arcon > Room > Room Settings
[CreateAssetMenu(fileName = "RoomSettings", menuName = "Arcon/Room/Room Settings")]
public class RoomSettings : ScriptableObject
{
    // ----- Dimensions -----

    [Header("Room Dimensions")]

    // Room width is constant across all rooms (20 units).
    public float Width = 20f;

    // Room length is chosen randomly between these bounds each generation.
    public float MinLength = 25f;
    public float MaxLength = 35f;

    // Wall height — how tall the perimeter walls are.
    public float WallHeight = 2.5f;

    // Physical thickness of each wall cube (used for corner overlap math).
    public float WallThickness = 1f;

    // ----- Colors -----

    [Header("Color Palette")]

    // Colors randomly picked each generation for the floor.
    // Leave empty to fall back to a random HSV color.
    public Color[] FloorColors = new Color[]
    {
        new Color(0.25f, 0.22f, 0.18f),  // dark stone
        new Color(0.30f, 0.28f, 0.25f),  // slate
        new Color(0.18f, 0.20f, 0.25f),  // cool grey-blue
        new Color(0.22f, 0.18f, 0.14f),  // warm clay
    };

    // Colors randomly picked each generation for the perimeter walls.
    public Color[] WallColors = new Color[]
    {
        new Color(0.35f, 0.31f, 0.26f),  // sandstone
        new Color(0.20f, 0.20f, 0.20f),  // charcoal
        new Color(0.28f, 0.25f, 0.35f),  // dusk purple
        new Color(0.24f, 0.30f, 0.22f),  // mossy green
    };

    // ----- Obstacles -----

    [Header("Obstacles")]

    // Pool of prefabs the generator can place as obstacles.
    public GameObject[] ObstaclePrefabs;

    // How many obstacles to scatter in the room.
    [Range(0, 30)] public int MinObstacles = 3;
    [Range(0, 30)] public int MaxObstacles = 8;

    // Clear zone at room center (radius) — no obstacles near the player spawn point.
    public float SpawnClearRadius = 3f;

    // How close obstacle placement attempts can be to a wall (inner boundary margin).
    public float ObstacleMarginFromWalls = 1.5f;

    // Minimum distance between any two obstacle centers.
    public float ObstacleMinSpacing = 1.5f;

    // Max placement retries per obstacle before giving up.
    public int MaxPlacementRetries = 20;
}
