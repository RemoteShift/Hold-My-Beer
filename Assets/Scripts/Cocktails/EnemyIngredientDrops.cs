using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyDropKind { Charger, Haymaker, ChairThrower, PowderKeg, PianoPlayer }

public readonly struct IngredientDropChance
{
    public readonly IngredientType Type;
    public readonly float Chance;
    public IngredientDropChance(IngredientType type, float chance) { Type = type; Chance = chance; }
}

public readonly struct IngredientDrop
{
    public readonly IngredientType Type;
    public readonly int Quantity;
    public IngredientDrop(IngredientType type, int quantity) { Type = type; Quantity = quantity; }
}

/// <summary>The GDD's per-unit rolls. Bonus units are independent, never conditional on an earlier roll.</summary>
public static class EnemyIngredientDrops
{
    private static readonly IngredientDropChance[][] Tables =
    {
        new[] { Chance(IngredientType.LifeEssence, 1f), Chance(IngredientType.Water, 0.6f) },
        new[] { Chance(IngredientType.LifeEssence, 1f), Chance(IngredientType.LifeEssence, 0.5f),
            Chance(IngredientType.LifeEssence, 0.2f), Chance(IngredientType.Water, 0.6f) },
        new[] { Chance(IngredientType.Sugar, 1f), Chance(IngredientType.Sugar, 0.6f), Chance(IngredientType.LifeEssence, 1f) },
        new[] { Chance(IngredientType.Gunpowder, 0.7f), Chance(IngredientType.Gunpowder, 0.5f), Chance(IngredientType.LifeEssence, 1f) },
        new[] { Chance(IngredientType.Sugar, 0.25f), Chance(IngredientType.LifeEssence, 1f),
            Chance(IngredientType.Gunpowder, 0.25f), Chance(IngredientType.Water, 0.25f) }
    };

    private static IngredientDropChance Chance(IngredientType type, float chance) => new IngredientDropChance(type, chance);

    public static IReadOnlyList<IngredientDropChance> GetTable(EnemyDropKind kind) => Array.AsReadOnly(Tables[(int)kind]);

    /// <summary>Inject a [0, 1] roll to verify exact probability boundaries without random tests.</summary>
    public static List<IngredientDrop> Roll(EnemyDropKind kind, Func<float> roll)
    {
        if (roll == null) throw new ArgumentNullException(nameof(roll));
        var amounts = new int[4];
        foreach (var entry in Tables[(int)kind])
            if (entry.Chance >= 1f || roll() < entry.Chance) amounts[(int)entry.Type]++;
        var drops = new List<IngredientDrop>(4);
        for (var i = 0; i < amounts.Length; i++)
            if (amounts[i] > 0) drops.Add(new IngredientDrop((IngredientType)i, amounts[i]));
        return drops;
    }

    public static void Spawn(EnemyBrain enemy)
    {
        EnemyDropKind kind;
        if (enemy is ChargerEnemy) kind = EnemyDropKind.Charger;
        else if (enemy is HaymakerEnemy) kind = EnemyDropKind.Haymaker;
        else if (enemy is ChairThrowerEnemy) kind = EnemyDropKind.ChairThrower;
        else if (enemy is PowderKegEnemy) kind = EnemyDropKind.PowderKeg;
        else if (enemy is PianoPlayerEnemy) kind = EnemyDropKind.PianoPlayer;
        else return;

        var drops = Roll(kind, () => UnityEngine.Random.value);
        for (var i = 0; i < drops.Count; i++)
        {
            var angle = i * 2.399963f;
            var position = enemy.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.4f;
            var closest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<EnemyBrain>() || hit.normal.y < 0.35f || hit.distance >= closest) continue;
                closest = hit.distance;
                position.y = hit.point.y;
            }
            IngredientPickup.Spawn(drops[i].Type, drops[i].Quantity, position + Vector3.up * 0.3f);
        }
    }
}
