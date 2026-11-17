using UnityEngine;

/// <summary>A full-radius ground ring persists for the effect lifetime.</summary>
public class CocktailSplash : MonoBehaviour
{
    private System.Collections.Generic.List<EnemyBrain> pushedEnemies;
    private Material material;
    private LineRenderer ring;
    private float born;
    private float lifetime;
    private float radius;
    private Color color;
    private Vector3 normal;

    public static void Create(Vector3 position, Vector3 normal, float radius, Color color, float seconds, System.Collections.Generic.List<EnemyBrain> pushedEnemies = null)
    {
        var go = new GameObject("Cocktail splash");
        go.transform.position = position;
        var splash = go.AddComponent<CocktailSplash>();
        splash.pushedEnemies = pushedEnemies;
        splash.born = Time.time;
        splash.lifetime = seconds;
        splash.radius = radius;
        splash.color = color;
        splash.normal = normal;
        splash.material = CocktailVisuals.CreateMaterial(Color.white);
        splash.ring = CocktailVisuals.CreateLine("Splash ring", go.transform, splash.material, 0.1f);
        CocktailVisuals.SetRing(splash.ring, position, normal, radius, color);
    }

    private void Update()
    {
        if (Time.time - born < lifetime) return;
        if (pushedEnemies != null)
            foreach (var enemy in pushedEnemies)
                if (enemy && !enemy.IsDead && enemy.isActiveAndEnabled && enemy.IsBeingPushed) return;
        Destroy(gameObject);
    }

    private void OnDestroy() { if (material) Destroy(material); }
}
