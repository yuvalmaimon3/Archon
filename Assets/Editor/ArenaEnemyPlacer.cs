using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time helper: places a SkeletonArcher prefab instance in the Arena scene.
// Run via: Tools > Arcon > Place SkeletonArcher in Arena
public static class ArenaEnemyPlacer
{
    private const string ArenaScenePath   = "Assets/Scenes/Arena.unity";
    private const string ArcherPrefabPath = "Assets/Prefabs/Enemies/SkeletonArcher.prefab";

    [MenuItem("Tools/Arcon/Place SkeletonArcher in Arena")]
    private static void PlaceArcher()
    {
        // Open Arena scene (additive if already loaded)
        var scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Single);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ArenaEnemyPlacer] Prefab not found at: {ArcherPrefabPath}");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(5f, 0f, 5f);
        instance.name = "SkeletonArcher";

        Undo.RegisterCreatedObjectUndo(instance, "Place SkeletonArcher");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ArenaEnemyPlacer] SkeletonArcher placed at (5, 0, 5) and scene saved.");
    }
}
