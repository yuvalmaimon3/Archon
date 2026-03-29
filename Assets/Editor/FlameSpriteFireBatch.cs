using UnityEditor;

/// <summary>
/// Unity.exe -batchmode -quit -projectPath "..." -executeMethod FlameSpriteFireBatch.BuildFromCommandLine
/// </summary>
public static class FlameSpriteFireBatch
{
    public static void BuildFromCommandLine()
    {
        FlameSpriteFireEffectCreator.PlacePrefabInLabScene();
        EditorApplication.Exit(0);
    }
}
