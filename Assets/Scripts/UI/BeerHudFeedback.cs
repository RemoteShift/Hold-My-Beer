using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only beer feedback. Never changes inventory or fill geometry.</summary>
public sealed class BeerHudFeedback : MonoBehaviour
{
    [SerializeField] private BeerInventory inventory;
    [SerializeField] private RectTransform[] slots;
    [SerializeField, Range(0f, 3f)] private float idleJitterDegrees = 0.55f;
    [SerializeField, Range(0f, 0.3f)] private float popStrength = 0.13f;
    [SerializeField, Min(0.1f)] private float feedbackDuration = 0.48f;
    [SerializeField, Range(0f, 0.25f)] private float refillStagger = 0.07f;
    private Vector3[] scales;
    private Quaternion[] rotations;
    private Image[][] images;
    private Color[][] colors;
    private float[] levels, ages;
    private int[] directions;
    private bool initialized;
    private float clock;

    private void OnEnable()
    {
        if (!inventory || slots == null) return;
        int count = slots.Length;
        scales = new Vector3[count];
        rotations = new Quaternion[count];
        images = new Image[count][];
        colors = new Color[count][];
        levels = new float[count];
        ages = new float[count];
        directions = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (!slots[i]) continue;
            scales[i] = slots[i].localScale;
            rotations[i] = slots[i].localRotation;
            images[i] = slots[i].GetComponentsInChildren<Image>(true);
            colors[i] = new Color[images[i].Length];
            for (int j = 0; j < images[i].Length; j++) colors[i][j] = images[i][j].color;
            levels[i] = i < inventory.Glasses.Count ? inventory.Glasses[i] : 0f;
            ages[i] = feedbackDuration;
        }
        initialized = true;
        inventory.OnChanged += OnInventoryChanged;
    }

    private void Start()
    {
        // Inventory.Awake may run after this component's OnEnable.
        if (!initialized) return;
        for (int i = 0; i < levels.Length; i++)
            levels[i] = i < inventory.Glasses.Count ? inventory.Glasses[i] : 0f;
    }

    private void OnDisable()
    {
        if (inventory) inventory.OnChanged -= OnInventoryChanged;
        if (!initialized) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i])
            {
                slots[i].localScale = scales[i];
                slots[i].localRotation = rotations[i];
            }
            if (images[i] == null) continue;
            for (int j = 0; j < images[i].Length; j++)
                if (images[i][j]) images[i][j].color = colors[i][j];
        }
        initialized = false;
    }

    private void OnInventoryChanged()
    {
        int gained = 0;
        for (int i = 0; i < levels.Length; i++)
        {
            float next = i < inventory.Glasses.Count ? Mathf.Clamp01(inventory.Glasses[i]) : 0f;
            if (!Mathf.Approximately(next, levels[i]))
            {
                directions[i] = next > levels[i] ? 1 : -1;
                ages[i] = directions[i] > 0 ? -refillStagger * gained++ : 0f;
            }
            levels[i] = next;
        }
    }

    private void Update() { Animate(Time.deltaTime); }

    private void Animate(float dt)
    {
        if (!initialized) return;
        if (dt <= 0f) return;
        clock += dt;
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i]) continue;
            ages[i] = Mathf.Min(feedbackDuration, ages[i] + dt);
            float t = Mathf.Clamp01(ages[i] / feedbackDuration);
            bool active = ages[i] >= 0f && ages[i] < feedbackDuration;
            float envelope = active ? Mathf.Sin(t * Mathf.PI) * (1f - t) : 0f;
            float idle = levels[i] > 0f ? idleJitterDegrees : 0f;
            if (levels[i] > 0f && levels[i] <= 0.25f) idle *= 1.8f;
            float wobble = idle * (Mathf.Sin(clock * 7f + i * 2.1f) * 0.65f
                + Mathf.Sin(clock * 13f + i * 3.7f) * 0.35f);
            float pulse = envelope * popStrength;
            float sx = 1f, sy = 1f;
            if (directions[i] > 0)
            {
                sx += pulse;
                sy += pulse * 1.6f;
                wobble += envelope * Mathf.Sin(t * Mathf.PI * 4f) * 5f;
            }
            else
            {
                sx += pulse * 0.65f;
                sy -= pulse;
                wobble += envelope * Mathf.Sin(t * Mathf.PI * 6f) * 8f;
            }
            slots[i].localScale = Vector3.Scale(scales[i], new Vector3(sx, sy, 1f));
            slots[i].localRotation = rotations[i] * Quaternion.Euler(0f, 0f, wobble);
            float flash = directions[i] > 0 ? envelope * 0.55f : 0f;
            for (int j = 0; j < images[i].Length; j++)
            {
                if (!images[i][j]) continue;
                Color warm = new Color(1f, 0.87f, 0.62f, colors[i][j].a);
                images[i][j].color = Color.Lerp(colors[i][j], warm, flash);
            }
        }
    }
}