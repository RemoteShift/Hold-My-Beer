using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BottleResultsEditor
{
    private const string PreviewKey = "HoldMyBeer.PreviewBottleResults";
    private const string Folder = "Assets/_Game/UI/Results";
    static BottleResultsEditor() { EditorApplication.playModeStateChanged += PlayModeChanged; }

    [MenuItem("Hold My Beer/Results/Preview Results %#r")]
    public static void Preview()
    {
        if (EditorApplication.isPlaying) { Find()?.Preview(); return; }
        if (!Find()) { Debug.LogWarning("Open Level 1, or add the Bottle Results Screen prefab to your scene first."); return; }
        SessionState.SetBool(PreviewKey, true);
        EditorApplication.isPlaying = true;
    }
    private static void PlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PreviewKey, false)) return;
        SessionState.SetBool(PreviewKey, false);
        EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) Find()?.Preview(); };
    }
    private static BottleResultsScreen Find() => Object.FindFirstObjectByType<BottleResultsScreen>();

    public static string InstallInCurrentScene()
    {
        if (EditorApplication.isPlaying) return "Exit Play Mode before installing.";
        var presenter = Find();
        if (presenter) return "Bottle results already installed.";
        var managers = Object.FindObjectsByType<LevelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        LevelManager realLevel = null;
        foreach (var manager in managers) if (manager.name == "Level Manager") realLevel = manager;
        var stats = Object.FindFirstObjectByType<RunStats>();
        if (!realLevel || !stats) return "Missing level manager or RunStats; no scene changes made.";
        var root = new GameObject("Bottle Results Screen");
        presenter = root.AddComponent<BottleResultsScreen>();
        var so = new SerializedObject(presenter);
        Set(so,"bottlePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Results Bottle.prefab"));
        Set(so,"emptyLabel",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/UI/Art/Menus/Results/Results_Menu_Empty.png"));
        Set(so,"checkStamp",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/UI/Art/Menus/Results/Results_Menu_Grade CheckBox.png"));
        Set(so,"numberFont",AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Folder+"/Results Numbers SDF.asset"));
        Set(so,"labelShader",Shader.Find("HoldMyBeer/Results/Label"));
        Set(so,"bottleShader",Shader.Find("HoldMyBeer/Results/Bottle"));
        Set(so,"celebrationShader",Shader.Find("HoldMyBeer/Results/Celebration"));
        Set(so,"filmLookProfile",AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Testing/Materials/Level 1 Film/Level 1 Film Grain.asset"));
        so.ApplyModifiedPropertiesWithoutUndo();
        var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,Folder+"/Bottle Results Screen.prefab",InteractionMode.AutomatedAction);
        so.Update(); Set(so,"levelManager",realLevel); Set(so,"stats",stats); so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject=root;
        return "Saved Bottle Results Screen prefab and wired Level 1 victory. Preview: Hold My Beer > Results > Preview Results, or F8 in Play Mode.";
    }
    private static void Set(SerializedObject so,string field,Object value) { so.FindProperty(field).objectReferenceValue=value; }
}
