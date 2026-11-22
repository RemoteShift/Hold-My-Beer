using UnityEngine;

/// <summary>A full-radius ground ring persists for the effect lifetime.</summary>
public class CocktailSplash : MonoBehaviour
{
    private System.Collections.Generic.List<EnemyBrain> pushedEnemies;
    private CocktailPixelRange ring;
    private CocktailPackEffect areaVfx;
    private float born;
    private float lifetime;
    private float radius;
    private Color color;
    private Vector3 normal;

    public static void Create(Vector3 position, Vector3 normal, float radius, Color color, float seconds, System.Collections.Generic.List<EnemyBrain> pushedEnemies = null, CocktailKind kind = CocktailKind.FizzySodaSplash)
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
        splash.ring = CocktailPixelRange.Create();
        if (splash.ring) splash.ring.transform.SetParent(go.transform, true);
        var tint = CocktailVfx.ColorFor(kind, color);
        if (splash.ring) splash.ring.Show(position, normal, radius, new Color(tint.r, tint.g, tint.b, .35f));
        splash.areaVfx = CocktailVfx.Area(kind, go.transform, normal, radius);
    }

    private void Update()
    {
        if (Time.time - born < lifetime) return;
        if (pushedEnemies != null)
            foreach (var enemy in pushedEnemies)
                if (enemy && !enemy.IsDead && enemy.isActiveAndEnabled && enemy.IsBeingPushed) return;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (areaVfx) areaVfx.Release();
    }
}
