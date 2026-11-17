using TMPro;
using UnityEngine;

/// <summary>A visible floor drop collected by walking close to it; walls prevent collection.</summary>
public sealed class IngredientPickup : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float pickupRadius = 1.8f;
    public IngredientType Type { get; private set; }
    public int Quantity { get; private set; }
    private IngredientInventory player;
    private Transform visual;
    private Transform label;
    private Vector3 restPosition;
    private Material material;
    private float nextPlayerSearch;
    private float collectAfter;
    private bool collected;

    public static IngredientPickup Spawn(IngredientType type, int quantity, Vector3 position)
    {
        var root = new GameObject(type + " drop (" + quantity + ")");
        root.transform.position = position;
        var pickup = root.AddComponent<IngredientPickup>();
        pickup.Type = type;
        pickup.Quantity = Mathf.Max(1, quantity);
        pickup.BuildVisual();
        return pickup;
    }

    private void BuildVisual()
    {
        restPosition = transform.position;
        collectAfter = Time.time + 0.25f;
        var body = GameObject.CreatePrimitive(Type == IngredientType.Sugar ? PrimitiveType.Cube :
            Type == IngredientType.Gunpowder ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
        body.name = "Ingredient";
        body.transform.SetParent(transform, false);
        body.transform.localScale = Type == IngredientType.Gunpowder ? new Vector3(0.24f, 0.16f, 0.24f) : Vector3.one * 0.28f;
        body.GetComponent<Collider>().enabled = false;
        visual = body.transform;
        var color = IngredientInfo.Color(Type);
        material = CocktailVisuals.CreateMaterial(color);
        body.GetComponent<Renderer>().sharedMaterial = material;

        var textObject = new GameObject("Ingredient label");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = Vector3.up * 0.4f;
        var text = textObject.AddComponent<TextMeshPro>();
        text.text = IngredientInfo.Name(Type) + " +" + Quantity;
        text.fontSize = 2.1f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        label = textObject.transform;
    }

    private void Update()
    {
        if (collected || Time.timeScale <= 0f) return;
        if (!player && Time.time >= nextPlayerSearch)
        {
            player = FindFirstObjectByType<IngredientInventory>();
            nextPlayerSearch = Time.time + 0.5f;
        }
        if (visual)
        {
            visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.5f + (int)Type) * 0.06f);
            visual.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        }
        if (!player || !player.isActiveAndEnabled || Time.time < collectAfter) return;
        var health = player.GetComponent<PlayerHealth>();
        if (health && health.IsDead) return;
        if ((player.transform.position - restPosition).sqrMagnitude > pickupRadius * pickupRadius) return;
        if (!CombatQuery.IsClear(restPosition, player.transform.position, transform, player.transform)) return;
        collected = true;
        player.Add(Type, Quantity);
        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        var camera = Camera.main;
        if (camera && label) label.rotation = camera.transform.rotation;
    }

    private void OnDestroy() { if (material) Destroy(material); }

}
