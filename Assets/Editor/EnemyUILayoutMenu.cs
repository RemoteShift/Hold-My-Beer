using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EnemyUILayoutMenu
{
    [MenuItem("Tools/Hold My Beer/Enemy UI/Health Above - Stamp Beside")]
    private static void HealthAbove() { SetLayout(EnemyUILayoutMode.HealthAboveStampBeside); }

    [MenuItem("Tools/Hold My Beer/Enemy UI/Stamp Above - Health Beside")]
    private static void StampAbove() { SetLayout(EnemyUILayoutMode.StampAboveHealthBeside); }

    [MenuItem("Tools/Hold My Beer/Enemy UI/Select Layout Settings")]
    private static void SelectSettings()
    {
        var settings = Resources.Load<EnemyUILayoutSettings>("EnemyUILayoutSettings");
        if (settings) { Selection.activeObject = settings; EditorGUIUtility.PingObject(settings); }
    }

    private static void SetLayout(EnemyUILayoutMode mode)
    {
        var settings = Resources.Load<EnemyUILayoutSettings>("EnemyUILayoutSettings");
        if (!settings) return;
        Undo.RecordObject(settings, "Change Enemy UI Layout");
        settings.layout = mode;
        EditorUtility.SetDirty(settings);
        settings.ApplyToLoadedEnemies();
        if (!EditorApplication.isPlaying)
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        SceneView.RepaintAll();
    }
}
