using UnityEngine;

/// <summary>Stepped, native-pixel HUD recoil when health is lost.</summary>
[DisallowMultipleComponent]
public sealed class PlayerHealthHudFeedback : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private RectTransform[] visuals;
    [SerializeField, Range(8, 24)] private int framesPerSecond = 14;

    private Vector2[] restPositions;
    private Vector2[] pixelSteps;
    private float previousHealth, previousFraction;
    private float age;
    private int appliedFrame = -1;
    private bool animating, cached;

    public void Configure(PlayerHealth source, RectTransform[] targets)
    {
        health = source;
        visuals = targets;
    }

    private void OnEnable()
    {
        if (!health || visuals == null) return;
        restPositions = new Vector2[visuals.Length];
        pixelSteps = new Vector2[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
        {
            var visual = visuals[i];
            if (!visual) continue;
            restPositions[i] = visual.anchoredPosition;
            var badge = visual.Find("Badge");
            var image = badge ? badge.GetComponent<UnityEngine.UI.Image>() : null;
            pixelSteps[i] = image && image.sprite
                ? new Vector2(image.rectTransform.rect.width / image.sprite.rect.width * visual.localScale.x,
                    image.rectTransform.rect.height / image.sprite.rect.height * visual.localScale.y)
                : Vector2.one;
        }
        previousHealth = health.CurrentHealth;
        previousFraction = Fraction(previousHealth, health.MaxHealth);
        cached = true;
        health.OnHealthChanged += OnHealthChanged;
    }

    private void Start()
    {
        // PlayerHealth.Awake can run after this component's OnEnable.
        if (!health) return;
        previousHealth = health.CurrentHealth;
        previousFraction = Fraction(previousHealth, health.MaxHealth);
    }

    private static float Fraction(float current, float maximum)
    {
        return maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
    }

    private void OnHealthChanged(float current, float maximum)
    {
        float fraction = Fraction(current, maximum);
        // A proportional max-health buff change is not a damage event.
        bool lostHealth = current < previousHealth - 0.001f && fraction < previousFraction - 0.0001f;
        bool gainedHealth = current > previousHealth + 0.001f;
        previousHealth = current;
        previousFraction = fraction;
        if (lostHealth)
        {
            age = 0f;
            appliedFrame = -1;
            animating = true;
            ApplyFrame(0);
        }
        else if (gainedHealth && animating)
        {
            Restore();
        }
    }

    private void LateUpdate()
    {
        if (!animating) return;
        // HUD feedback remains readable during hit-stop, then restores even if the game pauses.
        age += Time.unscaledDeltaTime;
        int frame = Mathf.FloorToInt(age * framesPerSecond);
        if (frame >= 8) Restore();
        else ApplyFrame(frame);
    }

    private void ApplyFrame(int frame)
    {
        if (!cached || appliedFrame == frame) return;
        appliedFrame = frame;
        // Each offset is an integer artwork pixel. Recoil goes inward from the HUD margins.
        Vector2 offset;
        switch (frame)
        {
            case 0: offset = new Vector2(3, 1); break;
            case 1: offset = new Vector2(0, 2); break;
            case 2: offset = new Vector2(2, 0); break;
            case 3: offset = new Vector2(0, 1); break;
            case 4: offset = new Vector2(1, 0); break;
            default: offset = Vector2.zero; break;
        }
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i]) visuals[i].anchoredPosition = restPositions[i] + Vector2.Scale(offset, pixelSteps[i]);
    }

    private void Restore()
    {
        if (!cached) return;
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i]) visuals[i].anchoredPosition = restPositions[i];
        animating = false;
        appliedFrame = -1;
    }

    private void OnDisable()
    {
        if (health) health.OnHealthChanged -= OnHealthChanged;
        Restore();
        cached = false;
    }
}
