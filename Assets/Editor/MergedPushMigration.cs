using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Idempotently wires the unified push system in first-party prefabs and test scenes.</summary>
public static class MergedPushMigration
{
    [MenuItem("Hold My Beer/Repair Merged Push References")]
    public static void Repair()
    {
        var count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game", "Assets/_Testing/Prefabs" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!RepairRoot(root)) continue;
                PrefabUtility.SaveAsPrefabAsset(root, path); count++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var active = SceneManager.GetActiveScene();
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Testing/Scenes" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = SceneManager.GetSceneByPath(path);
            var alreadyOpen = scene.IsValid() && scene.isLoaded;
            if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var changed = false;
            foreach (var root in scene.GetRootGameObjects()) changed |= RepairRoot(root);
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); count++; }
            if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
        }
        if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        AssetDatabase.SaveAssets();
        Debug.Log($"Merged push references repaired in {count} assets/scenes.");
    }

    private static bool RepairRoot(GameObject root)
    {
        var changed = false;
        foreach (var beer in root.GetComponentsInChildren<BeerHandler>(true))
        {
            if (!beer.GetComponent<PlayerAim>()) { beer.gameObject.AddComponent<PlayerAim>(); changed = true; }
            if (!beer.GetComponent<PushHandler>()) { beer.gameObject.AddComponent<PushHandler>(); changed = true; }
        }
        foreach (var prop in root.GetComponentsInChildren<Pushable>(true))
        {
            if (prop.GetComponent<ThrowableFurniture>()) continue;
            prop.gameObject.AddComponent<ThrowableFurniture>(); changed = true;
        }
        foreach (var hud in root.GetComponentsInChildren<BeerHud>(true))
        {
            var data = new SerializedObject(hud);
            var beer = data.FindProperty("handler").objectReferenceValue as BeerHandler;
            if (!beer) continue;
            var push = beer.GetComponent<PushHandler>();
            if (data.FindProperty("pushHandler").objectReferenceValue == push) continue;
            data.FindProperty("pushHandler").objectReferenceValue = push;
            data.ApplyModifiedPropertiesWithoutUndo(); changed = true;
        }
        return changed;
    }
}
