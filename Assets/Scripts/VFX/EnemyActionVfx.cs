using System.Collections.Generic;
using UnityEngine;

/// <summary>Action presentation follows the active combat state, never hit success.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class EnemyActionVfx : MonoBehaviour
{
    [SerializeField] private EnemyBrain enemy;
    [SerializeField] private Transform swingTip;
    private readonly List<CombatPixelEffect> chargePuffs = new List<CombatPixelEffect>();
    private CombatPixelEffect swoosh;

    private Vector3 previousTip;
    private float distanceSinceDust;
    private float nextDustTime;
    private bool wasCharging;
    private bool nextFootRight;

    private void OnEnable() { previousTip = swingTip ? swingTip.position : transform.position; }

    // Called in the movement step, before a same-frame hit can finish the charge.
    public void OnChargeMoved(float distance)
    {
        if (distance <= 0.01f || !isActiveAndEnabled || !enemy || !enemy.isActiveAndEnabled
            || enemy.IsDead || enemy.IsBeered || enemy.IsImpactStunned || enemy.IsBeingPushed) return;
        bool firstStep = !wasCharging;
        wasCharging = true;
        distanceSinceDust += distance;
        if (firstStep || (distanceSinceDust >= 0.4f && Time.time >= nextDustTime))
        {
            var puff = CombatVfx.ChargeDust(enemy, firstStep ? -0.45f : (nextFootRight ? 0.45f : -0.45f));
            if (puff) chargePuffs.Add(puff);
            if (firstStep)
            {
                var otherFoot = CombatVfx.ChargeDust(enemy, 0.45f);
                if (otherFoot) chargePuffs.Add(otherFoot);
            }
            nextFootRight = !nextFootRight;
            distanceSinceDust = 0f;
            nextDustTime = Time.time + 0.1f;
        }
    }
    private void LateUpdate()
    {
        bool alive = enemy && enemy.isActiveAndEnabled && !enemy.IsDead;
        bool charging = alive && enemy is ChargerEnemy && enemy.CurrentState is ChargerChargeState
            && enemy.CanNavigate && !enemy.IsBeingPushed && !enemy.IsImpactStunned;
        chargePuffs.RemoveAll(effect => !effect);
        if (!charging && wasCharging)
        {
            bool interrupted = !alive || enemy.IsBeered || enemy.IsImpactStunned
                || enemy.IsBeingPushed || enemy.CurrentState is EnemyStunState;
            if (interrupted) ClearChargeDust();
            else chargePuffs.Clear(); // A completed charge leaves its short dust puffs to dissipate.
            distanceSinceDust = 0f;
            wasCharging = false;
        }

        var haymaker = enemy as HaymakerEnemy;
        bool swinging = alive && haymaker && haymaker.IsSwinging && !enemy.IsImpactStunned;
        if (swinging && swingTip)
        {
            if (!swoosh) swoosh = CombatVfx.SwingArc(swingTip);
            if (swoosh)
            {
                swoosh.SampleProgress(haymaker.SwingProgress);
                var movement = swingTip.position - previousTip;
                swoosh.SetDirection(movement.sqrMagnitude > 0.00001f ? movement : haymaker.transform.right);
            }
        }
        else ClearSwing();
        previousTip = swingTip ? swingTip.position : transform.position;
    }

    private void ClearChargeDust()
    {
        foreach (var effect in chargePuffs) if (effect) Destroy(effect.gameObject);
        chargePuffs.Clear();
    }
    private void ClearSwing() { if (swoosh) Destroy(swoosh.gameObject); swoosh = null; }
    private void OnDisable() { ClearChargeDust(); ClearSwing(); wasCharging = false; distanceSinceDust = 0f; }
}
