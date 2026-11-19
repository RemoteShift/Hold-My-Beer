using TMPro;
using UnityEngine;

/// <summary>World-space countdown above an enemy, showing how long their beer will hold them.</summary>
public class EnemyFreezeTimer : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform fillBar;
    [SerializeField] private TMP_Text secondsText;
    [SerializeField] private UnityEngine.UI.Image stampBeer;
    [SerializeField, Min(0.0001f)] private float stampWorldScale = 0.008f;

    [SerializeField] private EnemyUILayoutSettings layoutSettings;
    [SerializeField, Min(0f)] private float sideOffset = 1f;
    [SerializeField] private float sideHeight = 0.35f;

    private EnemyUILayoutSettings Settings
    {
        get
        {
            if (!layoutSettings) layoutSettings = Resources.Load<EnemyUILayoutSettings>("EnemyUILayoutSettings");
            return layoutSettings;
        }
    }

    private bool IsBeside => Settings && Settings.layout == EnemyUILayoutMode.HealthAboveStampBeside;
    private void OnEnable() { RefreshLayout(); }

    public void RefreshLayout()
    {
        PrepareStamp();
        PositionStamp();
    }

    // Old scene-instance overrides must not restore the rectangular timer or tint the artwork.
    private void PrepareStamp()
    {
        if (!stampBeer || !panel || !fillBar) return;
        var background = panel.GetComponent<UnityEngine.UI.Image>();
        var fill = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (!background || !background.sprite || !fill) return;
        ((RectTransform)transform).sizeDelta = background.sprite.rect.size;
        StretchStampLayer(panel.GetComponent<RectTransform>());
        StretchStampLayer(fillBar);
        background.color = Color.white;
        background.type = UnityEngine.UI.Image.Type.Simple;
        background.preserveAspect = true;
        fill.color = Color.white;
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
        fill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
        fill.preserveAspect = true;
        stampBeer.color = Color.white;
        stampBeer.type = UnityEngine.UI.Image.Type.Simple;
        stampBeer.preserveAspect = true;
        var mug = stampBeer.rectTransform;
        mug.anchorMin = mug.anchorMax = mug.pivot = new Vector2(0.5f, 0.5f);
        mug.anchoredPosition = new Vector2(2f, 0f);
        mug.localScale = Vector3.one;
        stampBeer.SetNativeSize();
        if (secondsText) secondsText.gameObject.SetActive(false);
        SetStampScale();
    }

    private static void StretchStampLayer(RectTransform layer)
    {
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.pivot = new Vector2(0.5f, 0.5f);
        layer.offsetMin = layer.offsetMax = Vector2.zero;
        layer.localScale = Vector3.one;
        layer.localRotation = Quaternion.identity;
    }

    private void SetStampScale(float perspectiveScale = 1f)
    {
        if (!stampBeer) return;
        // Counter the enemy's inherited scale after billboarding.
        transform.localScale = Vector3.one;
        var inherited = transform.lossyScale;
        float scale = Settings ? (IsBeside ? Settings.sideStampPixelScale : Settings.overheadStampPixelScale) : stampWorldScale;
        scale *= perspectiveScale;
        transform.localScale = new Vector3(
            scale / Mathf.Max(0.0001f, Mathf.Abs(inherited.x)),
            scale / Mathf.Max(0.0001f, Mathf.Abs(inherited.y)),
            scale / Mathf.Max(0.0001f, Mathf.Abs(inherited.z)));
    }

    private void PositionStamp()
    {
        if (!enemy) return;
        var camera = Camera.main;
        if (camera) transform.rotation = camera.transform.rotation;
        if (!IsBeside)
        {
            if (Settings) transform.position = enemy.transform.TransformPoint(Vector3.up * Settings.overheadHeight);
            SetStampScale();
            return;
        }
        if (!camera) { SetStampScale(); return; }
        var anchor = enemy.transform.position + Vector3.up * sideHeight + camera.transform.right * sideOffset;
        float depth = Vector3.Dot(anchor - camera.transform.position, camera.transform.forward);
        float forwardOffset = Mathf.Min(0.75f, Mathf.Max(0f, depth * 0.5f));
        transform.position = anchor - camera.transform.forward * forwardOffset;
        float perspectiveScale = camera.orthographic || depth <= 0f ? 1f : (depth - forwardOffset) / depth;
        SetStampScale(perspectiveScale);
    }

    private void LateUpdate()
    {
        PositionStamp();
        if (!enemy || !panel || !fillBar) return;
        var isBeered = enemy.IsBeered;
        if (panel.activeSelf != isBeered) panel.SetActive(isBeered);
        if (!isBeered) return;

        var fillImage = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (fillImage && fillImage.type == UnityEngine.UI.Image.Type.Filled)
        {
            // Crop whole source rows; keep the stamp's pixels at their original proportions.
            var rows = fillImage.sprite ? fillImage.sprite.rect.height : fillBar.rect.height;
            var fill = Mathf.Clamp01(enemy.BeerFill);
            fillImage.fillAmount = rows > 0f ? Mathf.Ceil(fill * rows) / rows : fill;
        }
        else
        {
            fillBar.anchorMax = new Vector2(enemy.BeerFill, 1f);
        }
        if (secondsText && secondsText.gameObject.activeSelf)
            secondsText.text = $"{enemy.BeerSecondsLeft:0.0}s";



    }
}
