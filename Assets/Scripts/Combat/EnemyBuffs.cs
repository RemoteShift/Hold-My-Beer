using System;
using UnityEngine;

/// <summary>Each music effect has its own Inspector checkbox and multiplier.</summary>
[Serializable]
public class EnemyBuffSetting
{
    public bool enabled;
    [Min(1f)] public float multiplier = 1.35f;
    public float Value => enabled ? Mathf.Max(1f, multiplier) : 1f;
    public EnemyBuffSetting(bool enabled = false, float multiplier = 1.35f)
    { this.enabled = enabled; this.multiplier = multiplier; }
}

public struct EnemyBuffs
{
    public float moveSpeed, turnSpeed, windupSpeed, attackSpeed, recoverySpeed, damage, defense, maxHealth;
    public static EnemyBuffs None => new EnemyBuffs
    {
        moveSpeed = 1f, turnSpeed = 1f, windupSpeed = 1f, attackSpeed = 1f,
        recoverySpeed = 1f, damage = 1f, defense = 1f, maxHealth = 1f
    };
}
