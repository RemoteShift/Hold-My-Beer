using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small unlit cocktail visuals. Materials belong to the creating component.</summary>
public static class CocktailVisuals
{
    public static Material CreateMaterial(Color color)
    {
        var shader = Resources.Load<Shader>("CocktailVisual");
        if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader) { name = "Cocktail visual (runtime)" };
        material.SetColor("_BaseColor", color);
        return material;
    }

    public static LineRenderer CreateLine(string name, Transform parent, Material material, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCapVertices = 3;
        line.numCornerVertices = 3;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    public static void SetRing(LineRenderer line, Vector3 center, Vector3 normal, float radius, Color color)
    {
        const int segments = 64;
        line.positionCount = segments;
        line.loop = true;
        line.startColor = line.endColor = color;
        var tangent = Vector3.Cross(normal, Vector3.forward);
        if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
        tangent.Normalize();
        var bitangent = Vector3.Cross(normal, tangent).normalized;
        for (var i = 0; i < segments; i++)
        {
            var angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, center + normal * 0.035f +
                (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius);
        }
    }

    public static void CreateBottle(Transform parent, Material material)
    {
        Primitive("Bottle", PrimitiveType.Sphere, parent, new Vector3(0f, 0f, 0f),
            new Vector3(0.22f, 0.29f, 0.22f), material);
        Primitive("Neck", PrimitiveType.Cylinder, parent, new Vector3(0f, 0.19f, 0f),
            new Vector3(0.085f, 0.07f, 0.085f), material);
    }

    private static void Primitive(string name, PrimitiveType kind, Transform parent, Vector3 position,
        Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(kind);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
}
