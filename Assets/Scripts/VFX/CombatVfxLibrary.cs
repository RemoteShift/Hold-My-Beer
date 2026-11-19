using UnityEngine;

public sealed class CombatVfxLibrary : ScriptableObject
{
    public CombatPixelEffect deathPoof;
    public CombatPixelEffect heavyImpact;
    public CombatPixelEffect bluntImpact;
    public CombatPixelEffect explosion;
    public CombatPixelEffect fuseFlame;
    public CombatPixelEffect chargeDust;
    public CombatPixelEffect punchSwoosh;
    public CombatPixelEffect landingDust;



}

/// <summary>Uses pack prefabs; all spawned effects are independent of the victim's lifetime.</summary>
public static class CombatVfx
{
    private static CombatVfxLibrary library;
    public static CombatVfxLibrary Library
    {
        get { if (!library) library = Resources.Load<CombatVfxLibrary>("CombatVfxLibrary"); return library; }
    }

    private static CombatPixelEffect Spawn(CombatPixelEffect prefab, Vector3 position, float width = 0f)
    {
        if (!prefab || !Application.isPlaying) return null;
        var effect = Object.Instantiate(prefab, position, Quaternion.identity);
        if (width > 0f) effect.SetWorldWidth(width);
        return effect;
    }

    public static void Death(EnemyBrain enemy)
    {
        if (Library) Spawn(Library.deathPoof, enemy.transform.position);
    }

    public static void BluntImpact(Vector3 point)
    {
        if (Library) Spawn(Library.bluntImpact, point);
    }

    public static void BluntHit(Collider victim, Vector3 origin)
    {
        if (victim) BluntImpact(victim.ClosestPoint(origin));
    }

    public static bool ChargeObstacle(Transform charger, Vector3 direction)
    {
        // NavMesh edges alone are not collisions. Require actual scenery/furniture ahead.
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(charger.position, 0.45f, direction, 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(charger) || hit.collider.GetComponentInParent<EnemyBrain>()) continue;
            if (Vector3.Dot(hit.normal, direction) > -0.15f) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (!nearest.HasValue) return false;
        if (Library) Spawn(Library.heavyImpact, nearest.Value.point);
        return true;
    }

    public static void Explosion(EnemyBrain enemy, float radius)
    {
        if (!Library) return;
        var collider = enemy.GetComponent<Collider>();
        var point = enemy.transform.position;
        if (collider) point.y = collider.bounds.min.y + 0.03f;
        Spawn(Library.explosion, point, Mathf.Clamp(radius * 0.9f, 2f, 6f));
    }
    public static CombatPixelEffect ChargeDust(EnemyBrain enemy, float footOffset = 0f)
    {
        if (!Library) return null;
        var body = enemy.GetComponent<Collider>();
        var point = enemy.transform.position - enemy.transform.forward * 0.6f + enemy.transform.right * footOffset;
        if (body) point.y = body.bounds.min.y + 0.08f;
        var effect = Spawn(Library.chargeDust, point);
        // Keep floor dust upright: a projected forward vector can rotate it below the floor.
        return effect;
    }

    public static CombatPixelEffect SwingArc(Transform fist)
    {
        if (!Library || !fist) return null;
        var effect = Spawn(Library.punchSwoosh, fist.position);
        if (effect) { effect.Follow(fist); effect.Paused = true; }
        return effect;
    }

    public static void LandingDust(Vector3 point, float mass)
    {
        if (Library) Spawn(Library.landingDust, point + Vector3.up * 0.025f, Mathf.Clamp(0.7f + mass * 0.02f, 0.8f, 1.3f));
    }





}
