using UnityEngine;

/// <summary>Shows cocktail status loops for exactly as long as the actual enemy state lasts.</summary>
[DisallowMultipleComponent]
public sealed class CocktailEnemyVfx : MonoBehaviour
{
    private EnemyBrain enemy;
    private Collider body;
    private CocktailPackEffect honey, soda, aggression;

    public static void Ensure(EnemyBrain target)
    {
        if (target && !target.GetComponent<CocktailEnemyVfx>()) target.gameObject.AddComponent<CocktailEnemyVfx>();
    }
    private void Awake() { enemy = GetComponent<EnemyBrain>(); body = GetComponent<Collider>(); }
    private void LateUpdate()
    {
        bool alive = enemy && enemy.isActiveAndEnabled && !enemy.IsDead;
        float feet = body ? body.bounds.min.y - enemy.transform.position.y + .05f : -1f;
        UpdateStatus(ref honey, alive && enemy.IsHoneyRooted, CocktailKind.HoneyBrew, feet);
        UpdateStatus(ref soda, alive && enemy.IsBeingPushed, CocktailKind.FizzySodaSplash, feet);
        UpdateStatus(ref aggression, alive && enemy.IsAggressive, CocktailKind.AggressionTonic, feet);
        if (aggression) aggression.SetFollowOffset(CocktailVfx.AngerOffset(enemy));
    }
    private void UpdateStatus(ref CocktailPackEffect effect, bool visible, CocktailKind kind, float feet)
    {
        if (effect && !effect.IsPlaying) effect = null;
        if (visible && !effect) effect = CocktailVfx.Status(kind, enemy, feet);
        else if (!visible && effect) { effect.Release(); effect = null; }
    }
    private void OnDisable()
    {
        if (honey) honey.Release();
        if (soda) soda.Release();
        if (aggression) aggression.Release();
        honey = soda = aggression = null;
    }
}
