using UnityEngine;

/// <summary>Q selection lift and a small directional, stepped selector bump.</summary>
[DisallowMultipleComponent]
public sealed class CocktailHudFeedback : MonoBehaviour
{
    [SerializeField] private CocktailBrewer brewer;
    [SerializeField] private RectTransform stamp;
    [SerializeField, Min(0f)] private float lift = 12f;
    [SerializeField, Range(1f, 1.2f)] private float selectedScale = 1.03f;
    [SerializeField, Min(.01f)] private float transitionSeconds = .14f;
    [Header("Directional scroll snap")]
    [SerializeField] private CocktailStampView currentStamp;
    [SerializeField, Min(.05f)] private float scrollSeconds = .16f;
    [SerializeField, Min(1f)] private float scrollPixelStep = 4f;
    private Vector2 restPosition, currentRestPosition;
    private Vector3 restScale;
    private float blend, clock, scrollAge;
    private int scrollDirection;
    private bool cached, scrolling;

    private void OnEnable()
    {
        if (!stamp) return;
        restPosition = stamp.anchoredPosition;
        restScale = stamp.localScale;
        if (currentStamp) currentRestPosition = ((RectTransform)currentStamp.transform).anchoredPosition;
        blend = clock = scrollAge = 0f;
        scrolling = false;
        cached = true;
        if (brewer) brewer.OnSelectionScrolled += BeginScroll;
    }

    private void BeginScroll(int direction)
    {
        if (!cached || !currentStamp || !brewer || brewer.SelectedRecipe == null) return;
        currentStamp.SetKind(brewer.SelectedRecipe.Id);
        scrollDirection = direction >= 0 ? 1 : -1;
        scrollAge = 0f;
        scrolling = true;
        ApplyScrollPose();
    }

    private void Update() { Animate(Time.deltaTime); }

    private void Animate(float dt)
    {
        if (!cached || !stamp || dt <= 0f) return;
        bool selected = brewer && brewer.isActiveAndEnabled && brewer.IsCrafting;
        blend = Mathf.MoveTowards(blend, selected ? 1f : 0f, dt / transitionSeconds);
        clock = (clock + dt) % 600f;
        float frame = Mathf.Floor(clock * 12f) / 12f;
        float bob = selected ? Mathf.Round(Mathf.Sin(frame * 3.2f) * 2f) : 0f;
        stamp.anchoredPosition = restPosition + Vector2.up * ((lift + bob) * blend);
        stamp.localScale = restScale * Mathf.Lerp(1f, selectedScale, blend);
        if (!scrolling) return;
        scrollAge += dt;
        if (scrollAge >= scrollSeconds)
        {
            scrolling = false;
            ResetScrollPose();
        }
        else ApplyScrollPose();
    }

    private void ApplyScrollPose()
    {
        int pose = Mathf.Clamp(Mathf.FloorToInt(scrollAge / scrollSeconds * 4f), 0, 3);
        int pixelSteps = pose == 0 ? -3 : pose == 1 ? -1 : pose == 2 ? 1 : 0;
        ((RectTransform)currentStamp.transform).anchoredPosition = currentRestPosition
            + Vector2.up * (scrollDirection * scrollPixelStep * pixelSteps);
    }

    private void ResetScrollPose()
    {
        if (currentStamp) ((RectTransform)currentStamp.transform).anchoredPosition = currentRestPosition;
    }

    private void OnDisable()
    {
        if (brewer) brewer.OnSelectionScrolled -= BeginScroll;
        scrolling = false;
        ResetScrollPose();
        if (cached && stamp)
        {
            stamp.anchoredPosition = restPosition;
            stamp.localScale = restScale;
        }
        cached = false;
    }
}
