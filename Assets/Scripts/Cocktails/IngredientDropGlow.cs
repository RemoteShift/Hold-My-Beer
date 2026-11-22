using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A small pixel-stepped halo behind the camera-facing pickup art.</summary>
public sealed class IngredientDropGlow : MonoBehaviour
{
    private Mesh mesh;

    public static void Create(Transform icon)
    {
        if (!icon) return;
        var material = Resources.Load<Material>("IngredientDropGlow");
        if (!material) return;
        var go = new GameObject("Pickup glow");
        go.transform.SetParent(icon, false);
        go.transform.localPosition = Vector3.forward * .02f;
        go.transform.localScale = Vector3.one * .82f;
        var glow = go.AddComponent<IngredientDropGlow>();
        glow.mesh = new Mesh { name = "Pickup glow quad" };
        glow.mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0),
            new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
        glow.mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        glow.mesh.triangles = new[] { 0,1,2,0,2,3 };
        glow.mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = glow.mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var properties = new MaterialPropertyBlock();
        var pickup = icon.GetComponentInParent<IngredientPickup>();
        Color tint;
        switch (pickup ? pickup.Type : IngredientType.Sugar)
        {
            case IngredientType.LifeEssence: tint = new Color(3.5f, .25f, 1.5f, 1f); break;
            case IngredientType.Water: tint = new Color(.1f, 2.5f, 3.5f, 1f); break;
            case IngredientType.Gunpowder: tint = new Color(1.8f, .5f, 3.5f, 1f); break;
            default: tint = new Color(3.5f, 2.6f, .55f, 1f); break;
        }
        properties.SetColor("_GlowColor", tint);
        properties.SetFloat("_Phase", icon.position.x * 1.7f + icon.position.z * .9f);
        renderer.SetPropertyBlock(properties);
    }

    private void OnDestroy()
    {
        if (mesh) Destroy(mesh);
    }
}
