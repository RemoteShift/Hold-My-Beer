using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A pixel-grid circle on the ground, sized from the actual gameplay radius.</summary>
public sealed class CocktailPixelRange : MonoBehaviour
{
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock properties;
    public float Radius { get; private set; }
    public Vector3 Center { get; private set; }

    public static CocktailPixelRange Create()
    {
        var material = Resources.Load<Material>("CocktailPixelRange");
        if (!material) return null;
        var go = new GameObject("Cocktail pixel landing radius");
        var marker = go.AddComponent<CocktailPixelRange>();
        marker.mesh = new Mesh { name = "Pixel range quad" };
        marker.mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0),
            new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
        marker.mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        marker.mesh.triangles = new[] { 0,1,2,0,2,3 };
        marker.mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = marker.mesh;
        marker.meshRenderer = go.AddComponent<MeshRenderer>();
        marker.meshRenderer.sharedMaterial = material;
        marker.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        marker.meshRenderer.receiveShadows = false;
        marker.properties = new MaterialPropertyBlock();
        marker.Hide();
        return marker;
    }

    public void Show(Vector3 center, Vector3 normal, float radius, Color color)
    {
        Center = center;
        Radius = Mathf.Max(.01f, radius);
        var forward = Vector3.ProjectOnPlane(Vector3.forward, normal);
        if (forward.sqrMagnitude < .001f) forward = Vector3.right;
        transform.SetPositionAndRotation(center + normal * .05f, Quaternion.LookRotation(normal, forward));
        transform.localScale = new Vector3(Radius * 2f, Radius * 2f, 1f);
        properties.SetColor("_Tint", color.linear);
        meshRenderer.SetPropertyBlock(properties);
        gameObject.SetActive(true);
    }

    public void Hide() { gameObject.SetActive(false); }
    private void OnDestroy() { if (mesh) Destroy(mesh); }
}
