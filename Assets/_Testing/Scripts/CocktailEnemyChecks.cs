#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Integration checks for GDD drop boundaries, real enemy states and physical pickup spawning.</summary>
public sealed class CocktailEnemyChecks : MonoBehaviour
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly List<GameObject> suspended = new List<GameObject>();
    private readonly HashSet<IngredientPickup> existingDrops = new HashSet<IngredientPickup>();
    private NavMeshData navigation;
    private NavMeshDataInstance navigationInstance;
    private int assertions;
    private readonly Vector3 origin = new Vector3(600f, 100f, 600f);

    [MenuItem("Hold My Beer/Run Cocktail Enemy Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("Enter Play mode before running cocktail enemy checks."); return; }
        new GameObject("Cocktail Enemy Checks").AddComponent<CocktailEnemyChecks>();
    }

    private IEnumerator Start()
    {
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        { suspended.Add(enemy.gameObject); enemy.gameObject.SetActive(false); }
        foreach (var pickup in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None)) existingDrops.Add(pickup);
        try
        {
            CheckRolls();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(floor);
            floor.name = "Cocktail test floor";
            floor.transform.position = origin;
            floor.transform.localScale = new Vector3(30f, 0.1f, 30f);
            var source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(origin, Quaternion.identity, Vector3.one),
                size = floor.transform.localScale,
                area = 0
            };
            navigation = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),
                new List<NavMeshBuildSource> { source }, new Bounds(origin, new Vector3(40f, 10f, 40f)),
                Vector3.zero, Quaternion.identity);
            Check(navigation, "Isolated navigation builds");
            navigationInstance = NavMesh.AddNavMeshData(navigation);
            Check(NavMesh.SamplePosition(origin, out var center, 2f, NavMesh.AllAreas), "Test floor has walkable navigation");
            var playerTarget = new GameObject("Original player target"); created.Add(playerTarget);
            playerTarget.transform.position = center.position + Vector3.left * 8f;
            var charger = Spawn<ChargerEnemy>("Charger", center.position, playerTarget.transform);
            charger.detectRange = 0f;
            var victim = Spawn<HaymakerEnemy>("Haymaker", center.position + Vector3.right * 6f, playerTarget.transform);
            victim.detectRange = 0f;
            yield return null;

            charger.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            charger.SwitchState(new ChargerChargeState(charger));
            var chargeState = charger.CurrentState;
            var before = charger.transform.position;
            charger.ApplyHoney(0.4f);
            yield return new WaitForSeconds(0.2f);
            Check(charger.IsHoneyRooted && ReferenceEquals(chargeState, charger.CurrentState), "Honey preserves the charge state without a full stun");
            Check(Vector3.Distance(before, charger.transform.position) < 0.02f, "Honey blocks manual charge movement");
            yield return new WaitForSeconds(0.3f);
            Check(!charger.IsHoneyRooted && Vector3.Distance(before, charger.transform.position) > 0.1f, "Charge resumes when honey expires");
            charger.agent.Warp(center.position);
            charger.windupDuration = 10f;
            charger.SwitchState(new ChargerWindupState(charger));
            Check(charger.TryReceiveBeer(1f), "Test charge accepts beer");
            var beerBefore = charger.BeerFill;
            var frozenRotation = charger.transform.rotation;
            charger.ApplyHoney(0.5f);
            charger.ApplyAggression(0.5f);
            Check(charger.target == victim.transform, "Aggression immediately chooses another living patron");
            Check(charger.IsBeered && Quaternion.Angle(charger.transform.rotation, frozenRotation) < 0.01f, "Aggression does not unfreeze a beered pose");
            yield return new WaitForSeconds(0.15f);
            Check(charger.IsBeered && charger.BeerFill < beerBefore, "Honey does not pause the beer timer");
            charger.TryTakeBackBeer(out _);
            var towardVictim = victim.transform.position - charger.transform.position; towardVictim.y = 0f;
            Check(Vector3.Angle(charger.transform.forward, towardVictim) < 1f, "Unbeering redirects the pending attack to the aggression target");
            yield return new WaitForSeconds(0.45f);
            Check(!charger.IsAggressive && charger.target == playerTarget.transform, "Aggression restores the original target on expiry");
            var pianist = Spawn<PianoPlayerEnemy>("Piano Player", center.position, playerTarget.transform);
            pianist.musicRadius = 20f;
            pianist.moveSpeedBuff = new EnemyBuffSetting(true, 1.7f);
            pianist.defenseBuff = new EnemyBuffSetting(true, 2f);
            yield return null;
            Check(pianist.IsPlayingMusic, "Pianist plays before tonic");
            Check(Mathf.Approximately(victim.Buffs.moveSpeed, 1.7f), "Normal music buffs enemies");
            Check(Mathf.Approximately(PianoPlayerEnemy.GetPlayerBuffs(playerTarget.transform).moveSpeed, 1f), "Normal music does not buff player");
            pianist.ApplyAggression(0.3f);
            Check(pianist.IsPlayingMusic, "Tonic keeps the pianist playing");
            Check(Mathf.Approximately(victim.Buffs.moveSpeed, 1f), "Tonic removes enemy music benefits");
            Check(Mathf.Approximately(PianoPlayerEnemy.GetPlayerBuffs(playerTarget.transform).moveSpeed, 1.7f), "Tonic redirects movement buff to player");
            Check(Mathf.Approximately(PianoPlayerEnemy.GetPlayerBuffs(playerTarget.transform).defense, 2f), "Tonic redirects optional defense buff");
            playerTarget.transform.position += Vector3.left * 30f;
            Check(Mathf.Approximately(PianoPlayerEnemy.GetPlayerBuffs(playerTarget.transform).moveSpeed, 1f), "Player must remain in music radius");
            playerTarget.transform.position -= Vector3.left * 30f;
            yield return new WaitForSeconds(0.4f);
            Check(Mathf.Approximately(PianoPlayerEnemy.GetPlayerBuffs(playerTarget.transform).moveSpeed, 1f), "Player buff ends with tonic");
            Check(Mathf.Approximately(victim.Buffs.moveSpeed, 1.7f), "Enemy music resumes after tonic");
            pianist.gameObject.SetActive(false);
            charger.gameObject.SetActive(false); victim.gameObject.SetActive(false);

            var keg = Spawn<PowderKegEnemy>("Powder Keg", center.position + Vector3.back * 5f, playerTarget.transform);
            keg.target = keg.transform; keg.fuseSeconds = 10f;
            yield return null;
            Check(keg.IsLit, "Powder keg starts its fuse");
            keg.ApplyHoney(1f);
            var fuse = keg.FuseRemaining;
            yield return new WaitForSeconds(0.15f);
            Check(keg.IsHoneyRooted && keg.FuseRemaining < fuse, "Honey leaves an already lit fuse running");
            keg.gameObject.SetActive(false);

            var dropVictim = Spawn<ChargerEnemy>("Charger", center.position + Vector3.forward * 6f, playerTarget.transform);
            dropVictim.detectRange = 0f;
            yield return null;
            var initialCount = CountNewDrops();
            dropVictim.Die();
            var afterDeath = CountNewDrops();
            Check(afterDeath > initialCount, "A true death spawns visible ingredient pickups");
            dropVictim.Die();
            Check(CountNewDrops() == afterDeath, "Repeated death does not duplicate drops");
            Check(FindNewLifeDrop(), "Guaranteed Life Essence is present after charger death");
            var leaver = Spawn<HaymakerEnemy>("Haymaker", center.position + Vector3.forward * 9f, playerTarget.transform);
            leaver.Despawn();
            Check(CountNewDrops() == afterDeath, "Nonlethal despawn does not roll death loot");
            Debug.Log($"COCKTAIL ENEMY CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            foreach (var pickup in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
                if (!existingDrops.Contains(pickup)) Destroy(pickup.gameObject);
            foreach (var item in created) if (item) Destroy(item);
            if (navigationInstance.valid) navigationInstance.Remove();
            if (navigation) Destroy(navigation);
            foreach (var item in suspended) if (item) item.SetActive(true);
            Destroy(gameObject);
        }
    }

    private void CheckRolls()
    {
        var maximums = new[] { new[] { 1, 1, 0, 0 }, new[] { 3, 1, 0, 0 }, new[] { 1, 0, 2, 0 },
            new[] { 1, 0, 0, 2 }, new[] { 1, 1, 1, 1 } };
        var minimums = new[] { new[] { 1, 0, 0, 0 }, new[] { 1, 0, 0, 0 }, new[] { 1, 0, 1, 0 },
            new[] { 1, 0, 0, 0 }, new[] { 1, 0, 0, 0 } };
        var chances = new[] { new[] { 0.6f }, new[] { 0.5f, 0.2f, 0.6f }, new[] { 0.6f },
            new[] { 0.7f, 0.5f }, new[] { 0.25f, 0.25f, 0.25f } };
        for (var k = 0; k < 5; k++)
        {
            var kind = (EnemyDropKind)k;
            var maximum = Amounts(EnemyIngredientDrops.Roll(kind, () => 0f));
            var minimum = Amounts(EnemyIngredientDrops.Roll(kind, () => 1f));
            for (var i = 0; i < 4; i++)
            {
                Check(maximum[i] == maximums[k][i], kind + " all-success quantities match GDD");
                Check(minimum[i] == minimums[k][i], kind + " guaranteed quantities match GDD");
            }
            for (var rollIndex = 0; rollIndex < chances[k].Length; rollIndex++)
            {
                var counter = 0;
                var boundary = Amounts(EnemyIngredientDrops.Roll(kind, () =>
                    counter++ == rollIndex ? chances[k][rollIndex] : 1f));
                Check(Total(boundary) == Total(minimums[k]), kind + " probability rejects the exact boundary");
                counter = 0;
                var below = Amounts(EnemyIngredientDrops.Roll(kind, () =>
                    counter++ == rollIndex ? chances[k][rollIndex] - 0.0001f : 1f));
                Check(Total(below) == Total(minimums[k]) + 1, kind + " accepts below the boundary independently of earlier failures");
                Check(counter == chances[k].Length, kind + " rolls every optional unit independently");
            }
        }
    }

    private T Spawn<T>(string name, Vector3 position, Transform target) where T : EnemyBrain
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + name + ".prefab");
        var root = Instantiate(prefab, position, Quaternion.identity); created.Add(root);
        foreach (var detached in root.GetComponentsInChildren<DetachFurnitureOnStart>()) created.Add(detached.gameObject);
        var enemy = root.GetComponent<T>();
        enemy.target = target;
        enemy.agent.Warp(position);
        return enemy;
    }

    private int CountNewDrops()
    {
        var count = 0;
        foreach (var pickup in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
            if (!existingDrops.Contains(pickup)) count++;
        return count;
    }

    private bool FindNewLifeDrop()
    {
        foreach (var pickup in FindObjectsByType<IngredientPickup>(FindObjectsSortMode.None))
            if (!existingDrops.Contains(pickup) && pickup.Type == IngredientType.LifeEssence && pickup.Quantity == 1) return true;
        return false;
    }

    private static int[] Amounts(List<IngredientDrop> drops)
    {
        var values = new int[4];
        foreach (var drop in drops) values[(int)drop.Type] += drop.Quantity;
        return values;
    }
    private static int Total(int[] values) { var total = 0; foreach (var value in values) total += value; return total; }
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("COCKTAIL ENEMY CHECK FAILED: " + message);
        assertions++;
    }
}
#endif
