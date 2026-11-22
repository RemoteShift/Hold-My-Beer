using System.Collections.Generic;
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
    [SerializeField, Min(0.1f)] private float beerIconScale = 1.35f;
    [SerializeField] private Vector2 beerIconOffset = new Vector2(.65f, -1.12f);

    [SerializeField] private EnemyUILayoutSettings layoutSettings;
    [SerializeField, Min(0f)] private float sideOffset = 1f;
    [SerializeField] private float sideHeight = 0.35f;

    [Header("Cocktail effect stamps")]
    [SerializeField] private CocktailStampView honeyStamp, aggressionStamp, sodaStamp;
    [SerializeField, Min(104f)] private float stampSpacing = 118f;
    private readonly List<int> effectOrder = new List<int>(4);
    private float honeyDuration, honeyPrevious, aggressionDuration, aggressionPrevious;
    private float sodaDistance;

    public void ShowSodaPush()
    {
        sodaDistance = enemy && enemy.IsBeingPushed ? enemy.PushDistanceLeft : 0f;
    }

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
        mug.anchoredPosition = beerIconOffset;
        mug.localScale = Vector3.one;
        stampBeer.SetNativeSize();
        mug.sizeDelta *= beerIconScale;
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
        bool alive = !enemy.IsDead;
        SetEffectActive(0, alive && enemy.IsBeered);
        SetEffectActive(1, alive && enemy.IsHoneyRooted && honeyStamp);
        SetEffectActive(2, alive && enemy.IsAggressive && aggressionStamp);
        SetEffectActive(3, alive && sodaDistance > 0f && enemy.IsBeingPushed && sodaStamp);
        if (!enemy.IsBeingPushed) sodaDistance = 0f;

        if (honeyStamp)
            honeyStamp.SetProgress(TimerProgress(enemy.HoneySecondsLeft, ref honeyDuration, ref honeyPrevious));
        if (aggressionStamp)
            aggressionStamp.SetProgress(TimerProgress(enemy.AggressionSecondsLeft, ref aggressionDuration, ref aggressionPrevious));
        if (sodaStamp && sodaDistance > 0f)
            sodaStamp.SetProgress(enemy.PushDistanceLeft / sodaDistance);

        for (int i = 0; i < effectOrder.Count; i++)
        {
            var rect = EffectRect(effectOrder[i]);
            if (rect) rect.anchoredPosition = new Vector2((i % 2) * stampSpacing, -(i / 2) * stampSpacing);
        }
        if (!enemy.IsBeered) return;
        var fillImage = fillBar.GetComponent<UnityEngine.UI.Image>();
        if (fillImage && fillImage.type == UnityEngine.UI.Image.Type.Filled)
        {
            var rows = fillImage.sprite ? fillImage.sprite.rect.height : fillBar.rect.height;
            float fill = Mathf.Clamp01(enemy.BeerFill);
            fillImage.fillAmount = rows > 0f ? Mathf.Ceil(fill * rows) / rows : fill;
        }
        else fillBar.anchorMax = new Vector2(enemy.BeerFill, 1f);
        if (secondsText && secondsText.gameObject.activeSelf)
            secondsText.text = $"{enemy.BeerSecondsLeft:0.0}s";
    }

    private RectTransform EffectRect(int effect)
    {
        if (effect == 0) return panel ? panel.transform as RectTransform : null;
        var stamp = effect == 1 ? honeyStamp : effect == 2 ? aggressionStamp : sodaStamp;
        return stamp ? stamp.transform as RectTransform : null;
    }

    private void SetEffectActive(int effect, bool active)
    {
        var rect = EffectRect(effect);
        if (!rect) return;
        if (rect.gameObject.activeSelf != active) rect.gameObject.SetActive(active);
        int index = effectOrder.IndexOf(effect);
        if (active && index < 0) effectOrder.Add(effect);
        else if (!active && index >= 0) effectOrder.RemoveAt(index);
    }

    private static float TimerProgress(float remaining, ref float duration, ref float previous)
    {
        if (remaining <= 0f) { duration = previous = 0f; return 0f; }
        if (duration <= 0f || remaining > previous + .001f) duration = remaining;
        previous = remaining;
        return duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
    }

    private void OnDisable()
    {
        effectOrder.Clear();
        honeyDuration = honeyPrevious = aggressionDuration = aggressionPrevious = sodaDistance = 0f;
    }
}
