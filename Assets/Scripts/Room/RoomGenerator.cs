using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

// Procedurally builds a single room:
//  - Plane floor (20 x length), with a NavMeshSurface for enemy pathfinding
//  - 4 wall cubes forming a closed perimeter (no gaps at corners)
//  - Random perimeter colours chosen from RoomSettings palette
//  - Scattered random obstacles that sit flush on the floor
//
// Call GenerateRoom() at runtime to produce a new room layout.
// Call ClearRoom() to tear down the previous one before generating again.
[RequireComponent(typeof(NavMeshSurface))]
public class RoomGenerator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private RoomSettings settings;

    // ── Internal state ────────────────────────────────────────────────────────

    // Actual length chosen for this generation (width is always settings.Width).
    private float _roomLength;

    // References kept so ClearRoom() can destroy everything cleanly.
    private GameObject _floor;
    private readonly List<GameObject> _walls = new();
    private readonly List<GameObject> _obstacles = new();

    // NavMeshSurface is attached to this GameObject to bake navigation at runtime.
    private NavMeshSurface _navMeshSurface;

    // Placed obstacle positions, used for minimum-spacing checks.
    private readonly List<Vector3> _placedPositions = new();

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        _navMeshSurface = GetComponent<NavMeshSurface>();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Destroys any previous room and builds a fresh one.
    public void GenerateRoom()
    {
        ClearRoom();

        _roomLength = Random.Range(settings.MinLength, settings.MaxLength);

        Color floorColor = PickColor(settings.FloorColors);
        Color wallColor  = PickColor(settings.WallColors);

        _floor = BuildFloor(floorColor);
        BuildWalls(wallColor);
        SpawnObstacles();
        BakeNavMesh();

        Debug.Log($"[RoomGenerator] Generated room {settings.Width}x{_roomLength:F1}m, {_obstacles.Count} obstacles.");
    }

    // Destroys all generated room geometry.
    public void ClearRoom()
    {
        if (_floor != null) Destroy(_floor);

        foreach (var wall in _walls)     if (wall != null) Destroy(wall);
        foreach (var obs  in _obstacles) if (obs  != null) Destroy(obs);

        _walls.Clear();
        _obstacles.Clear();
        _placedPositions.Clear();

        _floor = null;
    }

    // ── Room construction ──────────────────────────────────────────────────────

    // Creates the floor plane scaled to (Width x _roomLength) at y=0.
    // Unity's default Plane is 10x10 world units, so scale accordingly.
    private GameObject BuildFloor(Color color)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(transform);
        floor.transform.localPosition = Vector3.zero;

        // Unity plane is 10x10 → divide target size by 10 for the scale factor.
        float scaleX = settings.Width  / 10f;
        float scaleZ = _roomLength     / 10f;
        floor.transform.localScale = new Vector3(scaleX, 1f, scaleZ);

        // Apply color material.
        ApplyColor(floor, color);

        // NavMeshSurface will use this plane's collider for baking — add it here.
        return floor;
    }

    // Creates four wall cubes that form a sealed perimeter around the floor.
    // North/South walls are extended to cover the corners where side walls meet.
    private void BuildWalls(Color color)
    {
        float w  = settings.Width;
        float l  = _roomLength;
        float h  = settings.WallHeight;
        float t  = settings.WallThickness;
        float hw = w / 2f;
        float hl = l / 2f;

        // North wall (+Z side) – extra width covers East/West wall corners.
        CreateWall("Wall_North", new Vector3(0f,       h / 2f, hl + t / 2f), new Vector3(w + 2f * t, h, t), color);

        // South wall (-Z side).
        CreateWall("Wall_South", new Vector3(0f,       h / 2f, -hl - t / 2f), new Vector3(w + 2f * t, h, t), color);

        // East wall (+X side) – spans only the interior length (no corner overlap needed; N/S handle it).
        CreateWall("Wall_East",  new Vector3(hw + t / 2f, h / 2f, 0f), new Vector3(t, h, l), color);

        // West wall (-X side).
        CreateWall("Wall_West",  new Vector3(-hw - t / 2f, h / 2f, 0f), new Vector3(t, h, l), color);
    }

    // Instantiates a single wall cube, sizes it, colours it, and parents it.
    private void CreateWall(string wallName, Vector3 localPos, Vector3 size, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform);
        wall.transform.localPosition = localPos;
        wall.transform.localScale    = size;

        ApplyColor(wall, color);
        _walls.Add(wall);
    }

    // ── Obstacles ──────────────────────────────────────────────────────────────

    // Scatters random obstacles inside the room, avoiding walls and the player
    // spawn clear-zone at the room centre.
    private void SpawnObstacles()
    {
        if (settings.ObstaclePrefabs == null || settings.ObstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("[RoomGenerator] No obstacle prefabs assigned in RoomSettings.");
            return;
        }

        int count = Random.Range(settings.MinObstacles, settings.MaxObstacles + 1);

        for (int i = 0; i < count; i++)
        {
            if (!TryPlaceObstacle())
                Debug.LogWarning($"[RoomGenerator] Obstacle {i} could not be placed after {settings.MaxPlacementRetries} retries.");
        }
    }

    // Attempts to find a valid position for one obstacle.
    // Returns true on success.
    private bool TryPlaceObstacle()
    {
        float hw     = settings.Width / 2f - settings.ObstacleMarginFromWalls;
        float hl     = _roomLength    / 2f - settings.ObstacleMarginFromWalls;

        for (int attempt = 0; attempt < settings.MaxPlacementRetries; attempt++)
        {
            // Random XZ inside the traversable region.
            float x = Random.Range(-hw, hw);
            float z = Random.Range(-hl, hl);
            var   candidate = new Vector3(x, 0f, z);

            // Skip positions too close to the player's spawn (room centre).
            if (Vector3.Distance(candidate, Vector3.zero) < settings.SpawnClearRadius)
                continue;

            // Skip positions too close to already-placed obstacles.
            if (IsTooClose(candidate))
                continue;

            PlaceObstacle(candidate);
            return true;
        }

        return false;
    }

    // Returns true if 'pos' is within ObstacleMinSpacing of any already-placed obstacle.
    private bool IsTooClose(Vector3 pos)
    {
        foreach (var placed in _placedPositions)
        {
            if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z),
                                 new Vector3(placed.x, 0f, placed.z)) < settings.ObstacleMinSpacing)
                return true;
        }
        return false;
    }

    // Instantiates a random obstacle prefab at the given XZ position
    // and raises it so its base sits flush on the floor (y = 0 + half-height).
    private void PlaceObstacle(Vector3 xzPosition)
    {
        int     idx     = Random.Range(0, settings.ObstaclePrefabs.Length);
        var     prefab  = settings.ObstaclePrefabs[idx];
        var     obs     = Instantiate(prefab, Vector3.zero, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
        obs.name = $"Obstacle_{_obstacles.Count}";

        // Measure the prefab's vertical extent so we can seat it on the floor.
        // We do this after instantiation so bounds reflect the actual mesh.
        float halfHeight = GetObstacleHalfHeight(obs);
        obs.transform.localPosition = new Vector3(xzPosition.x, halfHeight, xzPosition.z);

        _placedPositions.Add(xzPosition);
        _obstacles.Add(obs);
    }

    // Computes the half-height of an obstacle by sampling all its renderers.
    // Falls back to 0.5f if none found (object will still be visible above floor).
    private float GetObstacleHalfHeight(GameObject obs)
    {
        var renderers = obs.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0.5f;

        // Union all renderer bounds.
        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        return combined.extents.y;
    }

    // ── NavMesh ────────────────────────────────────────────────────────────────

    // Bakes the NavMesh so enemies can path-find around the generated obstacles.
    // Uses the NavMeshSurface on this GameObject.
    private void BakeNavMesh()
    {
        // Collect the surface for this room.
        _navMeshSurface.collectObjects = CollectObjects.Children;
        _navMeshSurface.BuildNavMesh();
        Debug.Log("[RoomGenerator] NavMesh baked.");
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    // Returns a random colour from the given palette,
    // or a random HSV colour if the palette is empty.
    private static Color PickColor(Color[] palette)
    {
        if (palette != null && palette.Length > 0)
            return palette[Random.Range(0, palette.Length)];

        // Fallback: random desaturated colour so rooms still look distinct.
        return Random.ColorHSV(0f, 1f, 0.2f, 0.5f, 0.3f, 0.7f);
    }

    // Creates a new material with the given color and applies it to the renderer.
    // We instantiate a fresh material per object so each room can have unique colors.
    private static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        // Use URP/Lit for consistency with the rest of the project.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        renderer.material = mat;
    }
}
