using UnityEngine;

/// <summary>Pixel-art health meter with shared overhead-horizontal and beside-vertical layouts.</summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private UnityEngine.UI.Image heartImage;
    [SerializeField] private EnemyUILayoutSettings layoutSettings;
    [SerializeField, Min(0.0001f)] private float healthWorldScale = 0.005f;
    [SerializeField, Min(0f)] private float sideOffset = 0.85f;
    [SerializeField] private float heightOffset = 0.05f;
    private EnemyUILayoutMode appliedLayout;
    private bool prepared;

    private EnemyUILayoutSettings Settings
    {
        get
        {
            if (!layoutSettings) layoutSettings = Resources.Load<EnemyUILayoutSettings>("EnemyUILayoutSettings");
            return layoutSettings;
        }
    }

    private bool IsHorizontal => Settings && Settings.layout == EnemyUILayoutMode.HealthAboveStampBeside;

    private void OnEnable() { RefreshLayout(); }

    public void RefreshLayout()
    {
        PrepareHealthBar();
        UpdateFill();
        PositionBar();
    }

    private void PrepareHealthBar()
    {
        var background = GetComponent<UnityEngine.UI.Image>();
        var settings = Settings;
        bool horizontal = IsHorizontal;
        if (settings)
        {
            var sprite = horizontal ? settings.horizontalHealthBackground : settings.verticalHealthBackground;
            if (background && sprite) background.sprite = sprite;
            if (fillBar && settings.healthFill) fillBar.GetComponent<UnityEngine.UI.Image>().sprite = settings.healthFill;
            if (heartImage && settings.healthHeart) heartImage.sprite = settings.healthHeart;
        }
        if (!background || !background.sprite || !fillBar || !heartImage) return;
        var fill = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (!fill || !fill.sprite || !heartImage.sprite) return;
        ((RectTransform)transform).sizeDelta = background.sprite.rect.size;
        background.color = Color.white;
        background.type = UnityEngine.UI.Image.Type.Simple;
        background.preserveAspect = true;
        background.raycastTarget = false;
        SetLayer(fill, horizontal ? new Vector2(1f, -0.5f) : new Vector2(0.5f, -0.5f));
        // Rotating the original bottom-filled image makes it drain from right to left.
        fill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, horizontal ? -90f : 0f);
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
        fill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
        SetLayer(heartImage, horizontal ? new Vector2(-77.5f, -0.5f) : new Vector2(0.5f, -79.5f));
        heartImage.type = UnityEngine.UI.Image.Type.Simple;
        fill.transform.SetAsFirstSibling();
        heartImage.transform.SetAsLastSibling();
        appliedLayout = settings ? settings.layout : EnemyUILayoutMode.StampAboveHealthBeside;
        prepared = true;
    }

    private static void SetLayer(UnityEngine.UI.Image image, Vector2 position)
    {
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        image.SetNativeSize();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void SetWorldScale(float perspectiveScale = 1f)
    {
        float pixelScale = Settings ? Settings.healthPixelScale : healthWorldScale;
        transform.localScale = Vector3.one;
        var inherited = transform.lossyScale;
        transform.localScale = new Vector3(
            pixelScale * perspectiveScale / Mathf.Max(0.0001f, Mathf.Abs(inherited.x)),
            pixelScale * perspectiveScale / Mathf.Max(0.0001f, Mathf.Abs(inherited.y)),
            pixelScale * perspectiveScale / Mathf.Max(0.0001f, Mathf.Abs(inherited.z)));
    }

    private void UpdateFill()
    {
        if (!enemy || !fillBar) return;
        var fill = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (fill && fill.sprite && fill.type == UnityEngine.UI.Image.Type.Filled)
        {
            if (!Application.isPlaying) { fill.fillAmount = 1f; return; }
            float rows = fill.sprite.rect.height;
            fill.fillAmount = Mathf.Ceil(enemy.HealthFraction * rows) / rows;
        }
        else fillBar.anchorMax = new Vector2(enemy.HealthFraction, 1f);
    }

    private void PositionBar()
    {
        if (!enemy) return;
        var camera = Camera.main;
        if (IsHorizontal)
        {
            transform.position = enemy.transform.TransformPoint(Vector3.up * Settings.horizontalHealthHeight);
            if (camera) transform.rotation = camera.transform.rotation;
            SetWorldScale();
            return;
        }
        if (!camera) { SetWorldScale(); return; }
        transform.rotation = camera.transform.rotation;
        var anchor = enemy.transform.position + Vector3.up * heightOffset + camera.transform.right * sideOffset;
        float depth = Vector3.Dot(anchor - camera.transform.position, camera.transform.forward);
        float forwardOffset = Mathf.Min(0.75f, Mathf.Max(0f, depth * 0.5f));
        transform.position = anchor - camera.transform.forward * forwardOffset;
        float perspectiveScale = camera.orthographic || depth <= 0f ? 1f : (depth - forwardOffset) / depth;
        SetWorldScale(perspectiveScale);
    }

    private void LateUpdate()
    {
        var settings = Settings;
        if (!prepared || (settings && appliedLayout != settings.layout)) PrepareHealthBar();
        UpdateFill();
        PositionBar();
    }
}
