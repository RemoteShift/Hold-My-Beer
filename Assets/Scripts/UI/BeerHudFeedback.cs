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
    [SerializeField, Range(0f, 2f)] private float refillFlashStrength = 0.8f;
    [SerializeField, Min(0.01f)] private float refillFlashSeconds = 0.18f;
    [Header("Selected beer")]
    [SerializeField, Min(0f)] private float selectedLift = 16f;
    [SerializeField, Range(1f, 1.3f)] private float selectedScale = 1.05f;
    [SerializeField, Range(20f, 30f)] private float selectedTilt = 25f;
    [SerializeField, Min(.01f)] private float selectionSeconds = .14f;
    private LayoutElement[] widthInputs;
    private HorizontalLayoutGroup rowLayout;
    private RectTransform rowRect;
    private Vector2 restRowSize;
    private Vector2[] restPositions;
    private float[] selectionWeights;
    public int SelectedSlotIndex { get; private set; } = -1;
    private BeerHandler handler;
    private float[] flashAges;
    private bool[] becameFull;
    private Material[] flashMaterials;
    private Material[][] originalMaterials;
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
        flashAges = new float[count];
        becameFull = new bool[count];
        flashMaterials = new Material[count];
        originalMaterials = new Material[count][];
        scales = new Vector3[count];
        restPositions = new Vector2[count];
        selectionWeights = new float[count];
        widthInputs = new LayoutElement[count];
        rowRect = count > 0 && slots[0] ? slots[0].parent.parent as RectTransform : null;
        rowLayout = rowRect ? rowRect.GetComponent<HorizontalLayoutGroup>() : null;
        restRowSize = rowRect ? rowRect.sizeDelta : Vector2.zero;
        rotations = new Quaternion[count];
        images = new Image[count][];
        colors = new Color[count][];
        levels = new float[count];
        ages = new float[count];
        directions = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (!slots[i]) continue;
            widthInputs[i] = slots[i].parent.GetComponent<LayoutElement>();
            scales[i] = slots[i].localScale;
            restPositions[i] = slots[i].anchoredPosition;
            rotations[i] = slots[i].localRotation;
            images[i] = slots[i].GetComponentsInChildren<Image>(true);
            colors[i] = new Color[images[i].Length];
            originalMaterials[i] = new Material[images[i].Length];
            flashAges[i] = refillFlashSeconds;
            if (images[i].Length > 0)
            {
                flashMaterials[i] = new Material(images[i][0].defaultMaterial)
                {
                    name = "Beer refill flash (runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            for (int j = 0; j < images[i].Length; j++)
            {
                colors[i][j] = images[i][j].color;
                originalMaterials[i][j] = images[i][j].material;
                images[i][j].material = flashMaterials[i];
            }
            levels[i] = LevelAt(i);
            ages[i] = feedbackDuration;
        }
        initialized = true;
        inventory.OnChanged += OnInventoryChanged;
        handler = inventory.GetComponent<BeerHandler>();
        if (handler) handler.OnRefilled += OnRefilled;
    }

    private void Start()
    {
        // Inventory.Awake may run after this component's OnEnable.
        if (!initialized) return;
        for (int i = 0; i < levels.Length; i++)
            levels[i] = LevelAt(i);
    }

    private void OnDisable()
    {
        if (inventory) inventory.OnChanged -= OnInventoryChanged;
        if (handler) handler.OnRefilled -= OnRefilled;
        if (!initialized) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i])
            {
                slots[i].anchoredPosition = restPositions[i];
                slots[i].localScale = scales[i];
                slots[i].localRotation = rotations[i];
                if (widthInputs[i])
                {
                    widthInputs[i].minWidth = slots[i].rect.width;
                    widthInputs[i].preferredWidth = slots[i].rect.width;
                }
            }
            if (images[i] == null) continue;
            for (int j = 0; j < images[i].Length; j++)
                if (images[i][j])
                {
                    images[i][j].color = colors[i][j];
                    images[i][j].material = originalMaterials[i][j];
                }
            if (flashMaterials[i])
            {
                if (Application.isPlaying) Destroy(flashMaterials[i]);
                else DestroyImmediate(flashMaterials[i]);
            }
        }
        if (rowRect && rowLayout)
        {
            rowRect.sizeDelta = restRowSize;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
        }
        initialized = false;
        SelectedSlotIndex = -1;
    }

    private float LevelAt(int visualIndex)
    {
        int inventoryIndex = slots.Length - 1 - visualIndex;
        return inventoryIndex >= 0 && inventoryIndex < inventory.Glasses.Count
            ? Mathf.Clamp01(inventory.Glasses[inventoryIndex]) : 0f;
    }

    private void OnInventoryChanged()
    {
        int gained = 0;
        for (int i = 0; i < levels.Length; i++)
        {
            float next = LevelAt(i);
            // Capture the transition before OnChanged updates our cached levels.
            becameFull[i] = levels[i] < 1f && next >= 1f;
            if (!Mathf.Approximately(next, levels[i]))
            {
                directions[i] = next > levels[i] ? 1 : -1;
                ages[i] = directions[i] > 0 ? -refillStagger * gained++ : 0f;
            }
            levels[i] = next;
        }
    }

    private void OnRefilled()
    {
        if (!initialized) return;
        // BeerHandler raises this only after a source refills inventory and OnChanged runs.
        int flashed = 0;
        for (int i = 0; i < flashAges.Length; i++)
        {
            flashAges[i] = slots[i] && becameFull[i]
                ? -refillStagger * flashed++
                : refillFlashSeconds;
            becameFull[i] = false;
        }
    }


    private void Update() { Animate(Time.deltaTime); }

    private void Animate(float dt)
    {
        if (!initialized) return;
        if (dt <= 0f) return;
        clock = (clock + dt) % 600f;
        SelectedSlotIndex = -1;
        for (int i = 0; i < levels.Length; i++)
            if (levels[i] > 0f) { SelectedSlotIndex = i; break; }
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
            selectionWeights[i] = Mathf.MoveTowards(selectionWeights[i],
                i == SelectedSlotIndex ? 1f : 0f, dt / selectionSeconds);
            float weight = selectionWeights[i];
            float selectionSize = Mathf.Lerp(1f, selectedScale, weight);
            slots[i].anchoredPosition = restPositions[i] + Vector2.up * (selectedLift * weight);
            slots[i].localScale = Vector3.Scale(scales[i], new Vector3(sx * selectionSize, sy * selectionSize, 1f));
            slots[i].localRotation = rotations[i] * Quaternion.Euler(0f, 0f, wobble + selectedTilt * weight);
            // Neutral HDR brightness preserves the artwork and alpha; no amber color overlay.
            flashAges[i] = Mathf.Min(refillFlashSeconds, flashAges[i] + dt);
            float flash = flashAges[i] >= 0f && flashAges[i] < refillFlashSeconds
                ? 1f - flashAges[i] / refillFlashSeconds : 0f;
            float brightness = 1f + refillFlashStrength * flash * flash;
            if (flashMaterials[i])
                flashMaterials[i].SetColor("_Color", new Color(brightness, brightness, brightness, 1f));
        }
        UpdateDynamicSpacing();
    }

    private void UpdateDynamicSpacing()
    {
        if (!rowLayout || !rowRect || widthInputs == null) return;
        float required = rowLayout.padding.horizontal + rowLayout.spacing * Mathf.Max(0, slots.Length - 1);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i] || !widthInputs[i]) continue;
            float angle = slots[i].localEulerAngles.z * Mathf.Deg2Rad;
            var size = slots[i].rect.size;
            var scale = slots[i].localScale;
            float projected = Mathf.Abs(Mathf.Cos(angle) * size.x * scale.x)
                + Mathf.Abs(Mathf.Sin(angle) * size.y * scale.y);
            float width = Mathf.Ceil(Mathf.Max(size.x, projected));
            if (!Mathf.Approximately(widthInputs[i].preferredWidth, width))
            {
                widthInputs[i].minWidth = width;
                widthInputs[i].preferredWidth = width;
            }
            required += width;
        }
        // The right pivot stays fixed. Extra space grows left, away from the cocktail.
        rowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(restRowSize.x, required));
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowRect);
    }
}