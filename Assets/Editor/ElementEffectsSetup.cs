using UnityEditor;
using UnityEngine;

// One-time setup: adds ElementStatusEffects and ElementVFXController to all enemy prefabs.
// Run via menu: Tools → Setup Element Effects on Enemies
public static class ElementEffectsSetup
{
    [MenuItem("Tools/Setup Element Effects on Enemies")]
    public static void AddElementEffectsToAllEnemies()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" });
        int added = 0;

        foreach (string guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = PrefabUtility.LoadPrefabContents(path);

            // Only process prefabs that have ElementStatusController (i.e. actual enemies)
            if (go.GetComponent<ElementStatusController>() == null)
            {
                PrefabUtility.UnloadPrefabContents(go);
                continue;
            }

            bool changed = false;

            if (go.GetComponent<ElementStatusEffects>() == null)
            {
                go.AddComponent<ElementStatusEffects>();
                changed = true;
            }

            if (go.GetComponent<ElementVFXController>() == null)
            {
                go.AddComponent<ElementVFXController>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(go, path);
                Debug.Log($"[ElementEffectsSetup] Added components to '{path}'");
                added++;
            }

            PrefabUtility.UnloadPrefabContents(go);
        }

        Debug.Log($"[ElementEffectsSetup] Done — updated {added} enemy prefab(s).");
        EditorUtility.DisplayDialog("Element Effects Setup", $"Done. Updated {added} enemy prefab(s).", "OK");
    }
}
