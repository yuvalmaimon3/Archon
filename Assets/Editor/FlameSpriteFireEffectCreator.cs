using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The flame-sprite fire lives as a normal prefab: <c>Assets/Effects/Prefabs/FX_FlameSpriteFire.prefab</c>.
/// This menu re-adds or refreshes an instance in <c>Assets/lab effects.unity</c> at world X = 12.
/// </summary>
public static class FlameSpriteFireEffectCreator
{
    private const string PrefabPath   = "Assets/Effects/Prefabs/FX_FlameSpriteFire.prefab";
    private const string LabScenePath = "Assets/lab effects.unity";

    /// <summary>
    /// Ensures the lab scene contains one <c>FX_FlameSpriteFire</c> prefab instance at (12, 0, 0).
    /// </summary>
    [MenuItem("Effects/Place Flame Sprite Fire In Lab Scene")]
    public static void PlacePrefabInLabScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[FlameSpriteFire] Missing prefab at '{PrefabPath}'.");
            return;
        }

        if (SceneManager.GetActiveScene().path != LabScenePath)
            EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);

        var existing = GameObject.Find("FX_FlameSpriteFire");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(12f, 0f, 0f);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = instance;
        Debug.Log($"[FlameSpriteFire] Placed '{PrefabPath}' in '{LabScenePath}' at (12, 0, 0).");
    }
}
