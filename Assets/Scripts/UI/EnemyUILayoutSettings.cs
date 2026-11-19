using UnityEngine;

public enum EnemyUILayoutMode
{
    HealthAboveStampBeside,
    StampAboveHealthBeside
}

[CreateAssetMenu(menuName = "Hold My Beer/Enemy UI Layout", fileName = "EnemyUILayoutSettings")]
public sealed class EnemyUILayoutSettings : ScriptableObject
{
    [Tooltip("Shared layout for all enemy health bars and drink stamps.")]
    public EnemyUILayoutMode layout = EnemyUILayoutMode.HealthAboveStampBeside;
    [Header("Artwork")]
    public Sprite verticalHealthBackground;
    public Sprite horizontalHealthBackground;
    public Sprite healthFill;
    public Sprite healthHeart;
    [Header("World Size and Placement")]
    [Min(0.0001f)] public float healthPixelScale = 0.005f;
    [Min(0.0001f)] public float overheadStampPixelScale = 0.008f;
    [Min(0.0001f)] public float sideStampPixelScale = 0.0055f;
    public float overheadHeight = 1.85f;
    [Tooltip("Height above the enemy origin for the horizontal health bar only.")]
    public float horizontalHealthHeight = 1.5f;

    [ContextMenu("Apply Layout to Loaded Enemies")]
    public void ApplyToLoadedEnemies()
    {
        foreach (var bar in FindObjectsByType<EnemyHealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            bar.RefreshLayout();
        foreach (var stamp in FindObjectsByType<EnemyFreezeTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            stamp.RefreshLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshEditorPreview;
        UnityEditor.EditorApplication.delayCall += RefreshEditorPreview;
    }

    private void RefreshEditorPreview()
    {
        if (!this || UnityEditor.EditorApplication.isCompiling) return;
        ApplyToLoadedEnemies();
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnDisable() { UnityEditor.EditorApplication.delayCall -= RefreshEditorPreview; }
#endif
}
