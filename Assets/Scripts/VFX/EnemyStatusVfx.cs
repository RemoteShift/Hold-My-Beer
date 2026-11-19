using UnityEngine;

/// <summary>Presentation only: follows the existing combat states and actual piano multipliers.</summary>
[DisallowMultipleComponent]
public sealed class EnemyStatusVfx : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private GameObject stunStars;
    [SerializeField] private GameObject beerBubbles;
    [SerializeField] private GameObject musicNotes;

    private void LateUpdate()
    {
        bool alive = enemy && enemy.isActiveAndEnabled && !enemy.IsDead;
        SetVisible(stunStars, alive && (enemy.IsImpactStunned || enemy.CurrentState is EnemyStunState));
        SetVisible(beerBubbles, alive && enemy.IsBeered);
        SetVisible(musicNotes, alive && HasPianoBuff(enemy.Buffs));
    }
    private void OnDisable()
    {
        SetVisible(stunStars, false);
        SetVisible(beerBubbles, false);
        SetVisible(musicNotes, false);
    }
    private static bool HasPianoBuff(EnemyBuffs buffs)
    {
        return buffs.moveSpeed > 1f || buffs.turnSpeed > 1f || buffs.windupSpeed > 1f
            || buffs.attackSpeed > 1f || buffs.recoverySpeed > 1f || buffs.damage > 1f
            || buffs.defense > 1f || buffs.maxHealth > 1f;
    }
    private static void SetVisible(GameObject effect, bool visible)
    {
        if (effect && effect.activeSelf != visible) effect.SetActive(visible);
    }
}
