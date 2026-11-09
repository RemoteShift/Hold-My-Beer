#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Play-mode integration checks against the actual prefabs. Run via Hold My Beer menu.</summary>
public class EnemyBehaviourChecks : MonoBehaviour
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly List<GameObject> suspended = new List<GameObject>();
    private bool regressionsOnly;
    private int assertions;
    private NavMeshData testNavigation;
    private NavMeshDataInstance navigationInstance;
    private Vector3 origin = new Vector3(200f, 100f, 200f);

    [MenuItem("Hold My Beer/Run Enemy Behaviour Checks (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("Enter Play mode before running enemy checks."); return; }
        new GameObject("Enemy Behaviour Checks").AddComponent<EnemyBehaviourChecks>();
    }

    [MenuItem("Hold My Beer/Run Impact and Charger Checks (Play Mode)")]
    public static void RunRegressions()
    {
        if (!Application.isPlaying) { Debug.LogError("Enter Play mode before running enemy checks."); return; }
        var checks = new GameObject("Impact and Charger Checks").AddComponent<EnemyBehaviourChecks>();
        checks.regressionsOnly = true;
    }

    private IEnumerator Start()
    {
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        { suspended.Add(enemy.gameObject); enemy.gameObject.SetActive(false); }
        try
        {
            var target = new GameObject("Test aim target"); created.Add(target);
            if (regressionsOnly)
            {
                var checks = CheckThrowablesAndCharger(target.transform);
                while (checks.MoveNext()) yield return checks.Current;
                Debug.Log($"IMPACT AND CHARGER CHECKS PASSED: {assertions} assertions.");
                yield break;
            }
            target.transform.position = origin + Vector3.forward * 2f;
            var hay = Spawn<HaymakerEnemy>("Haymaker", origin, target.transform);
            hay.windupSeconds = 0.6f;
            yield return null;
            Assert(hay.CanBeBeered, "Haymaker telegraphs inside hand range");
            var attack = hay.CurrentState;
            Assert(!hay.TryReceiveBeer(0f), "Empty beer is rejected");
            Assert(hay.TryReceiveBeer(0.8f), "Haymaker accepts beer");
            var pose = hay.swingPivot.localRotation;
            var facing = hay.transform.rotation;
            target.transform.position = origin + Vector3.left * 2f;
            yield return new WaitForSeconds(0.15f);
            Assert(hay.IsBeered && hay.swingPivot.localRotation == pose, "Frozen haymaker holds its fist pose");
            Assert(hay.TryTakeBackBeer(out var fill) && fill > 0f && fill < 0.8f, "Taking beer back returns remaining fill");
            Assert(ReferenceEquals(attack, hay.CurrentState), "Unbeer resumes the same swing state");
            yield return null;
            Assert(Quaternion.Angle(facing, hay.transform.rotation) < 0.01f, "Unbeer preserves swing aim");
            var victim = Spawn<ChargerEnemy>("Charger", origin + Vector3.forward * 2f, target.transform);
            victim.enabled = false;
            var health = victim.currentHealth;
            var rear = Spawn<ChargerEnemy>("Charger", origin - Vector3.forward * 2f, target.transform);
            rear.enabled = false;
            yield return new WaitForSeconds(1.2f);
            Assert(victim.currentHealth == health - hay.damage, "Haymaker damages a patron in its arc once");
            Assert(rear.currentHealth == rear.maxHealth, "Haymaker leaves patrons behind it unharmed");
            hay.gameObject.SetActive(false); victim.gameObject.SetActive(false); rear.gameObject.SetActive(false);

            target.transform.position = origin + Vector3.forward * 8f;
            var thrower = Spawn<ChairThrowerEnemy>("Chair Thrower", origin, target.transform);
            thrower.chargeSeconds = 0.5f;
            yield return null;
            Assert(thrower.HeldChair && thrower.HeldChair.IsHeld, "Thrower holds an actual chair");
            Assert(thrower.TryReceiveBeer(0.8f), "Throw windup can be beered");
            var chair = thrower.HeldChair; created.Add(chair.gameObject);
            var chairPose = chair.transform.position;
            yield return new WaitForSeconds(0.15f);
            Assert(chair.transform.position == chairPose && chair.Body.isKinematic, "Beer freezes the overhead chair");
            target.transform.position = origin + Vector3.left * 8f;
            thrower.TryTakeBackBeer(out _);
            yield return new WaitForSeconds(0.65f);
            Assert(!chair.IsHeld && !chair.Body.isKinematic, "Unbeer releases a physical projectile");
            Assert(chair.Body.linearVelocity.z > 5f && Mathf.Abs(chair.Body.linearVelocity.x) < 0.1f, "Throw follows locked aim instead of retargeting");
            thrower.gameObject.SetActive(false); chair.gameObject.SetActive(false);
            var doomedThrower = Spawn<ChairThrowerEnemy>("Chair Thrower", origin, target.transform);
            yield return null;
            var dropped = doomedThrower.HeldChair; created.Add(dropped.gameObject);
            doomedThrower.Die();
            Assert(dropped && !dropped.IsHeld && !dropped.Body.isKinematic, "Dying thrower drops its held weapon");
            dropped.gameObject.SetActive(false);

            target.transform.position = origin + Vector3.forward * 2f;
            var keg = Spawn<PowderKegEnemy>("Powder Keg", origin, target.transform);
            keg.fuseSeconds = 2f;
            yield return null;
            Assert(keg.IsLit && keg.CanBeBeered, "Keg lights its fuse on detection");
            keg.TryReceiveBeer(0.1f);
            var fuse = keg.FuseRemaining;
            yield return new WaitForSeconds(0.15f);
            Assert(Mathf.Approximately(keg.FuseRemaining, fuse), "Beer pauses the exact fuse time");
            yield return new WaitForSeconds(0.6f);
            yield return null;
            Assert(!keg.IsBeered && keg.FuseRemaining < fuse, "Empty beer automatically resumes the fuse");
            var blastVictim = Spawn<ChargerEnemy>("Charger", origin + Vector3.right * 2f, target.transform);
            blastVictim.maxHealth = 200f; blastVictim.RefreshHealthCapacity(); blastVictim.currentHealth = 200f; blastVictim.enabled = false;
            var protectedVictim = Spawn<ChargerEnemy>("Charger", origin + Vector3.forward * 3f, target.transform);
            protectedVictim.enabled = false;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(wall);
            wall.transform.position = origin + Vector3.forward * 1.5f;
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            var nearProp = Prop(origin + Vector3.left, false);
            var farProp = Prop(origin + Vector3.left * 4f, false);
            Physics.SyncTransforms();
            keg.TakeDamage(999f); keg.Die();
            Assert(keg.IsDead, "Lethal damage detonates the keg");
            Assert(blastVictim.currentHealth == 120f, "Explosion causes friendly fire exactly once");
            Assert(protectedVictim.currentHealth == protectedVictim.maxHealth, "Solid scenery blocks blast damage");
            wall.SetActive(false); protectedVictim.gameObject.SetActive(false);
            blastVictim.gameObject.SetActive(false);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert(nearProp.Body.linearVelocity.magnitude > farProp.Body.linearVelocity.magnitude,
                "Blast launches nearby furniture harder than distant furniture");
            Assert(farProp.Body.linearVelocity.magnitude > 0.1f && nearProp.Body.linearVelocity.y > 0f,
                "Blast moves distant furniture and lifts nearby furniture");
            nearProp.gameObject.SetActive(false); farProp.gameObject.SetActive(false);

            var pianist = Spawn<PianoPlayerEnemy>("Piano Player", origin, target.transform);
            var listener = Spawn<ChargerEnemy>("Charger", origin + Vector3.right * 3f, target.transform);
            listener.enabled = false;
            yield return null;
            Assert(listener.MoveSpeedMultiplier == pianist.moveSpeedBuff.multiplier, "Music buffs nearby enemies");
            Assert(pianist.music.isPlaying, "Pianist plays audible music");
            Assert(pianist.TryReceiveBeer(1f), "Music can be beered without a melee attack");
            Assert(listener.MoveSpeedMultiplier == 1f && !pianist.music.isPlaying, "Beer immediately stops music and buff");
            pianist.TryTakeBackBeer(out _);
            Assert(listener.MoveSpeedMultiplier > 1f && pianist.music.isPlaying, "Unbeer resumes music and buff");
            listener.transform.position = origin + Vector3.right * 30f;
            Assert(listener.MoveSpeedMultiplier == 1f, "Music has a limited radius");
            listener.transform.position = origin + Vector3.right * 3f;
            pianist.moveSpeedBuff.enabled = false;
            pianist.windupSpeedBuff.multiplier = 2f;
            pianist.attackSpeedBuff.multiplier = 3f;
            pianist.recoverySpeedBuff.multiplier = 4f;
            Assert(listener.MoveSpeedMultiplier == 1f && listener.Buffs.windupSpeed == 2f &&
                listener.Buffs.attackSpeed == 3f && listener.Buffs.recoverySpeed == 4f,
                "Movement, windup, attack and recovery multipliers are independent");
            pianist.turnSpeedBuff.enabled = true; pianist.turnSpeedBuff.multiplier = 2.5f;
            Assert(listener.Buffs.turnSpeed == 2.5f, "Turning has its own multiplier");
            pianist.damageBuff.enabled = true; pianist.damageBuff.multiplier = 2f;
            Assert(listener.EffectiveDamage == listener.damage * 2f, "Damage buff multiplies outgoing damage");
            pianist.defenseBuff.enabled = true; pianist.defenseBuff.multiplier = 2f;
            listener.currentHealth = 40f; listener.TakeDamage(20f);
            Assert(listener.currentHealth == 30f, "Defense buff divides incoming damage");
            pianist.maxHealthBuff.enabled = true; pianist.maxHealthBuff.multiplier = 2f;
            listener.RefreshHealthCapacity();
            Assert(listener.EffectiveMaxHealth == listener.maxHealth * 2f && listener.currentHealth == 60f,
                "HP buff increases capacity while preserving health percentage");
            pianist.maxHealthBuff.enabled = false; listener.RefreshHealthCapacity();
            Assert(listener.currentHealth == 30f, "Removing HP buff preserves wounds");
            pianist.maxHealthBuff.enabled = true; listener.RefreshHealthCapacity();
            Assert(listener.currentHealth == 60f, "Re-entering HP aura cannot heal wounds");
            pianist.TryReceiveBeer(1f); listener.RefreshHealthCapacity();
            Assert(listener.Buffs.damage == 1f && listener.Buffs.defense == 1f && listener.currentHealth == 30f &&
                listener.Buffs.attackSpeed == 1f && listener.Buffs.windupSpeed == 1f,
                "Beering pianist removes every enabled buff");
            pianist.Die();
            Assert(listener.MoveSpeedMultiplier == 1f, "Killing pianist immediately removes buff");
            listener.gameObject.SetActive(false);

            var impactVictim = Spawn<ChargerEnemy>("Charger", origin + Vector3.forward * 2f, target.transform);
            impactVictim.enabled = false;
            var furniture = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Combat/Pushable Stool.prefab"), origin, Quaternion.identity);
            created.Add(furniture);
            var prop = furniture.GetComponent<ThrowableFurniture>();
            prop.Body.useGravity = false;
            prop.Shove(Vector3.forward * 20f);
            yield return new WaitForSeconds(0.35f);
            Assert(impactVictim.currentHealth < impactVictim.maxHealth, "Shoved furniture damages patrons through physics collision");
            impactVictim.gameObject.SetActive(false); furniture.SetActive(false);
            var seatedVictim = Spawn<PianoPlayerEnemy>("Piano Player", origin, target.transform);
            var lamp = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Combat/Falling Chandelier.prefab"), origin + Vector3.up * 3f, Quaternion.identity);
            created.Add(lamp);
            yield return null;
            lamp.GetComponent<FallingChandelier>().Drop();
            Assert(!lamp.GetComponent<Rigidbody>().isKinematic, "Chandelier suspension can be released");
            yield return new WaitForSeconds(1.2f);
            Assert(!seatedVictim || seatedVictim.IsDead, "Falling chandelier defeats the pianist");
            if (seatedVictim) seatedVictim.gameObject.SetActive(false);
            lamp.SetActive(false);

            // Existing model pivots may be off-centre and physics may be interpolating a previous rotation.
            var holder = Spawn<ChairThrowerEnemy>("Chair Thrower", origin + Vector3.right * 20f, target.transform);
            holder.enabled = false;
            var heldProp = Prop(origin + Vector3.right * 20f, false);
            heldProp.transform.rotation = Quaternion.Euler(40f, 120f, 75f);
            heldProp.Body.interpolation = RigidbodyInterpolation.Interpolate;
            var grip = new GameObject("Test offset grip").transform;
            grip.SetParent(heldProp.transform, false);
            grip.localPosition = new Vector3(0.2f, 0.3f, -0.1f);
            grip.localRotation = Quaternion.Euler(0f, 35f, 0f);
            heldProp.gripPoint = grip;
            Assert(heldProp.TryHold(holder, holder.chairHoldPoint), "Rotated furniture can be picked up");
            yield return null;
            Assert(Vector3.Distance(grip.position, holder.chairHoldPoint.position) < 0.001f &&
                Quaternion.Angle(grip.rotation, holder.chairHoldPoint.rotation) < 0.01f,
                "Off-centre grip snaps position and rotation to the hold point");
            holder.transform.Rotate(0f, 70f, 0f);
            yield return new WaitForFixedUpdate(); yield return null;
            Assert(Vector3.Distance(grip.position, holder.chairHoldPoint.position) < 0.001f &&
                Quaternion.Angle(grip.rotation, holder.chairHoldPoint.rotation) < 0.01f,
                "Held pose survives physics ticks and holder rotation");
            heldProp.Release(Vector3.forward * 8f, 50f);
            Assert(heldProp.IsDangerous, "Release arms furniture");
            heldProp.gameObject.SetActive(false); holder.gameObject.SetActive(false);

            var safeProp = Prop(origin, false);
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(barrier);
            barrier.transform.position = origin + Vector3.forward * 1.5f;
            barrier.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();
            safeProp.Shove(Vector3.forward * 20f);
            yield return new WaitForSeconds(0.3f);
            Assert(!safeProp.IsDangerous, "Touching non-damageable scenery disarms furniture");
            barrier.SetActive(false);
            safeProp.Body.position = origin; safeProp.Body.rotation = Quaternion.identity;
            safeProp.Body.linearVelocity = Vector3.forward * 10f; safeProp.Body.angularVelocity = Vector3.zero;
            var safeVictim = Spawn<ChargerEnemy>("Charger", origin + Vector3.forward * 2f, target.transform);
            safeVictim.enabled = false;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.35f);
            Assert(safeVictim.currentHealth == safeVictim.maxHealth, "Disarmed furniture cannot damage enemies even while moving fast");
            safeProp.Shove(Vector3.right * 10f);
            Assert(safeProp.IsDangerous, "A new shove re-arms furniture");
            // Drive the nested checks here so a failed assertion still runs this suite's cleanup.
            var regressions = CheckThrowablesAndCharger(target.transform);
            while (regressions.MoveNext()) yield return regressions.Current;
            Debug.Log($"ENEMY CHECKS PASSED: {assertions} assertions.");
        }
        finally
        {
            if (navigationInstance.valid) navigationInstance.Remove();
            if (testNavigation) Destroy(testNavigation);
            foreach (var item in created) if (item) Destroy(item);
            foreach (var item in suspended) if (item) item.SetActive(true);
            Destroy(gameObject);
        }
    }

    private IEnumerator CheckThrowablesAndCharger(Transform target)
    {
        var point = origin + Vector3.right * 30f;
        var closeVictim = Spawn<ChargerEnemy>("Charger", point + Vector3.forward, target);
        closeVictim.enabled = false;
        var fastProp = Prop(point, false);
        fastProp.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        Physics.SyncTransforms();
        fastProp.Shove(Vector3.forward * 80f);
        Assert(fastProp.Body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
            "Arming a scene prop enables continuous collision detection");
        var impactDeadline = Time.time + 1f;
        while (closeVictim.currentHealth == closeVictim.maxHealth && Time.time < impactDeadline)
            yield return new WaitForFixedUpdate();
        yield return null;
        Assert(closeVictim.currentHealth == closeVictim.maxHealth - fastProp.impactDamage,
            $"A close-range shove damages an enemy on the first physics step exactly once (HP {closeVictim.currentHealth}/{closeVictim.maxHealth}, damage {fastProp.impactDamage}, dangerous {fastProp.IsDangerous}, speed {fastProp.Body.linearVelocity})");
        fastProp.gameObject.SetActive(false); closeVictim.gameObject.SetActive(false);

        var owner = Spawn<ChairThrowerEnemy>("Chair Thrower", point, target);
        owner.enabled = false;
        var projectile = owner.startingChair; created.Add(projectile.gameObject);
        projectile.TryHold(owner, owner.chairHoldPoint);
        var thrownVictim = Spawn<ChargerEnemy>("Charger", projectile.transform.position + Vector3.forward * 2f, target);
        thrownVictim.enabled = false;
        thrownVictim.gameObject.AddComponent<BoxCollider>();
        Physics.SyncTransforms();
        projectile.Release(Vector3.forward * 18f, 22f);
        impactDeadline = Time.time + 1f;
        while (thrownVictim.currentHealth == thrownVictim.maxHealth && Time.time < impactDeadline)
            yield return new WaitForFixedUpdate();
        yield return null;
        Assert(thrownVictim.currentHealth == thrownVictim.maxHealth - 22f,
            "A released chair damages an enemy with multiple colliders only once");
        owner.gameObject.SetActive(false); projectile.gameObject.SetActive(false); thrownVictim.gameObject.SetActive(false);

        // A small real NavMesh, independent of the level's bake, with a stationary target beyond its edge.
        var navOrigin = origin + Vector3.right * 70f;
        var source = new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            transform = Matrix4x4.TRS(navOrigin, Quaternion.identity, Vector3.one),
            size = new Vector3(20f, 0.1f, 20f), area = 0
        };
        var settings = NavMesh.GetSettingsByID(0);
        testNavigation = NavMeshBuilder.BuildNavMeshData(settings, new List<NavMeshBuildSource> { source },
            new Bounds(navOrigin, new Vector3(30f, 10f, 30f)), Vector3.zero, Quaternion.identity);
        Assert(testNavigation != null, "Built isolated navigation for charger regressions");
        navigationInstance = NavMesh.AddNavMeshData(testNavigation);
        Assert(NavMesh.SamplePosition(navOrigin, out var centre, 2f, NavMesh.AllAreas), "Test navigation is walkable");
        Assert(NavMesh.Raycast(centre.position, centre.position + Vector3.right * 20f, out var edge, NavMesh.AllAreas),
            "Found navigation edge for stationary-target test");
        var targetPosition = edge.position + Vector3.right * 0.25f + Vector3.up;
        var stationaryTarget = new GameObject("Stationary off-mesh target"); created.Add(stationaryTarget);
        stationaryTarget.transform.position = targetPosition;
        var charger = Spawn<ChargerEnemy>("Charger", edge.position - Vector3.right * 2f + Vector3.up, stationaryTarget.transform);
        charger.transform.rotation = Quaternion.LookRotation(Vector3.right);
        charger.agent.enabled = true;
        charger.agent.Warp(edge.position - Vector3.right * 2f);
        charger.windupDuration = 0.25f;
        charger.chargeDistance = 3f;
        Assert(charger.CanNavigate, "Charger uses a live NavMeshAgent");
        Assert(charger.agent.Raycast(targetPosition, out _), "Old endpoint NavMesh check rejects the visible target");
        Physics.SyncTransforms();
        Assert(charger.CanStartCharge(), "Stationary off-mesh target can trigger a charge");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(wall);
        wall.transform.position = (charger.transform.position + targetPosition) * 0.5f;
        wall.transform.localScale = new Vector3(0.2f, 4f, 4f);
        Physics.SyncTransforms();
        Assert(!charger.CanStartCharge(), "Physical walls still block charge initiation");
        wall.SetActive(false); Physics.SyncTransforms();
        yield return null;
        Assert(charger.CurrentState is ChargerWindupState, "Stationary target transitions chase into windup without moving");
        var initial = charger.transform.position;
        var deadline = Time.time + 0.8f;
        while (Time.time < deadline && Vector3.Distance(initial, charger.transform.position) < 0.3f) yield return null;
        Assert(Vector3.Distance(initial, charger.transform.position) >= 0.3f, "Charger executes its dash toward the stationary target");

        charger.agent.Warp(centre.position);
        stationaryTarget.transform.position = centre.position + Vector3.right * 4f + Vector3.up;
        charger.attackRange = 1f;
        charger.agent.stoppingDistance = 3f;
        charger.ReturnToDefaultState();
        charger.CurrentState.Update();
        Assert(charger.agent.stoppingDistance < charger.attackRange,
            "Chase stopping distance cannot leave the charger outside its attack range");
        charger.gameObject.SetActive(false);

        var collector = Spawn<ChairThrowerEnemy>("Chair Thrower", centre.position + Vector3.up, stationaryTarget.transform);
        collector.startingChair.gameObject.SetActive(false); collector.startingChair = null;
        collector.pickupReach = 1.2f;
        collector.agent.stoppingDistance = 3f;
        collector.chargeSeconds = 100f;
        collector.agent.enabled = true; collector.agent.Warp(centre.position);
        var lowStool = Prop(centre.position + Vector3.forward * 4f + Vector3.up * 0.3f, false);
        Physics.SyncTransforms();
        yield return null;
        Assert(collector.agent.stoppingDistance < collector.pickupReach,
            "Furniture retrieval overrides a combat stopping distance outside grab reach");
        deadline = Time.time + 4f;
        while (!collector.HeldChair && Time.time < deadline) yield return null;
        Assert(collector.HeldChair == lowStool, "Thrower approaches and picks up a low stationary stool without player help");
        Assert(Mathf.Approximately(collector.agent.stoppingDistance, 3f), "Pickup restores the combat stopping distance");
        collector.gameObject.SetActive(false);

        var offsetCollector = Spawn<ChairThrowerEnemy>("Chair Thrower", centre.position + Vector3.up, stationaryTarget.transform);
        offsetCollector.startingChair.gameObject.SetActive(false); offsetCollector.startingChair = null;
        offsetCollector.pickupReach = 1.2f; offsetCollector.pickupRadius = 10f;
        offsetCollector.chargeSeconds = 100f;
        offsetCollector.agent.enabled = true; offsetCollector.agent.Warp(centre.position);
        var offsetStool = Prop(centre.position + Vector3.right * 12f + Vector3.up * 0.3f, false);
        offsetStool.GetComponent<BoxCollider>().center += Vector3.left * 8f;
        Physics.SyncTransforms();
        Assert(Vector3.Distance(offsetCollector.transform.position, offsetStool.transform.position) > offsetCollector.pickupRadius,
            "Offset-pivot regression prop has its root outside the furniture search radius");
        deadline = Time.time + 4f;
        while (!offsetCollector.HeldChair && Time.time < deadline) yield return null;
        Assert(offsetCollector.HeldChair == offsetStool, "Furniture search and pickup use reachable collider geometry instead of the root pivot");
        offsetCollector.gameObject.SetActive(false);
        var mergeChecks = CheckMergedPushes(centre.position, stationaryTarget.transform);
        while (mergeChecks.MoveNext()) yield return mergeChecks.Current;
    }

    private IEnumerator CheckMergedPushes(Vector3 centre, Transform target)
    {
        var light = Spawn<ChargerEnemy>("Charger", centre + new Vector3(-4f, 1f, -3f), target);
        var heavy = Spawn<ChargerEnemy>("Charger", centre + new Vector3(-4f, 1f, 3f), target);
        light.mass = 5f; heavy.mass = 50f;
        foreach (var enemy in new[] { light, heavy })
        {
            enemy.detectRange = 0f; enemy.fullBeerFreezeSeconds = 20f;
            enemy.agent.enabled = true;
            enemy.agent.Warp(enemy.transform.position - Vector3.up);
        }
        yield return null;
        Assert(!light.TryPush(Vector3.right, 30f), "Unified player push rejects an unbeered enemy");
        foreach (var enemy in new[] { light, heavy })
        {
            enemy.SwitchState(new ChargerWindupState(enemy));
            Assert(enemy.TryReceiveBeer(1f), "Mass test patron can be beered before pushing");
        }
        var lightStart = light.transform.position;
        var heavyStart = heavy.transform.position;
        var rotation = light.transform.rotation;
        Assert(light.TryPush(Vector3.right * 9f, 30f) && heavy.TryPush(Vector3.right, 30f), "Common push accepts both enemy masses and normalizes direction");
        Assert(!light.TryPush(Vector3.right, 30f), "An enemy already sliding cannot receive a duplicate input push");
        var deadline = Time.time + 4f;
        while ((light.IsBeingPushed || heavy.IsBeingPushed) && Time.time < deadline) yield return null;
        Assert(!light.IsBeingPushed && !heavy.IsBeingPushed, "Completed pushes clear movement locks for both masses");
        var lightTravel = Vector3.Distance(lightStart, light.transform.position);
        var heavyTravel = Vector3.Distance(heavyStart, heavy.transform.position);
        Assert(Mathf.Abs(lightTravel - 6f) < 0.05f && Mathf.Abs(heavyTravel - 0.6f) < 0.05f,
            $"Mass 5 travels 6m while mass 50 travels 0.6m (actual {lightTravel:0.000}, {heavyTravel:0.000})");
        Assert(light.IsBeered && light.BeerFill < 1f && Quaternion.Angle(rotation, light.transform.rotation) < 0.01f,
            "Pushed patrons keep their frozen pose while beer continues draining");
        Assert(!heavy.TryPush(Vector3.zero, 30f) && !heavy.TryPush(Vector3.right, 0f), "Zero-direction and zero-strength pushes are rejected");
        heavy.KnockbackFrom(heavy.transform.position - Vector3.right, 0.0005f);
        yield return null; yield return null;
        Assert(!heavy.IsBeingPushed && heavy.PushDistanceLeft == 0f, "Sub-millimetre pushes clear their movement lock");
        heavy.SwitchState(new ChargerChaseState(heavy));
        heavy.detectRange = 100f;
        heavy.target.position = heavy.transform.position + Vector3.left * 5f;
        var resumeStart = heavy.transform.position;
        yield return new WaitForSeconds(0.5f);
        Assert(Vector3.Distance(resumeStart, heavy.transform.position) > 0.1f,
            "Enemy resumes chasing after its push and beer end");
        light.gameObject.SetActive(false); heavy.gameObject.SetActive(false);

        var slammed = Spawn<ChargerEnemy>("Charger", centre + new Vector3(-4f, 1f, -3f), target);
        slammed.agent.enabled = true;
        slammed.agent.Warp(slammed.transform.position - Vector3.up);
        slammed.fullBeerFreezeSeconds = 20f;
        slammed.stunDuration = 0.5f;
        yield return null;
        slammed.SwitchState(new ChargerWindupState(slammed));
        slammed.TryReceiveBeer(1f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(wall);
        wall.transform.position = slammed.transform.position + Vector3.right;
        wall.transform.localScale = new Vector3(0.2f, 3f, 3f);
        Physics.SyncTransforms();
        var slamHealth = slammed.currentHealth;
        Assert(slammed.TryPush(Vector3.right, 30f), "A beered enemy can be pushed");
        deadline = Time.time + 2f;
        while (slammed.IsBeingPushed && Time.time < deadline) yield return null;
        Assert(slammed.IsImpactStunned && slammed.IsBeered && slammed.currentHealth == slamHealth,
            "Wall impact stuns the pushed enemy without damaging it or removing its beer");
        var impactFill = slammed.BeerFill;
        yield return new WaitForSeconds(0.2f);
        Assert(slammed.BeerFill == impactFill && slammed.currentHealth == slamHealth,
            "Wall stun pauses beer drain and deals no damage");
        yield return new WaitForSeconds(0.4f);
        Assert(!slammed.IsImpactStunned && slammed.BeerFill < impactFill,
            "Beer drain resumes after impact stun expires");
        wall.SetActive(false);
        var struck = Spawn<ChargerEnemy>("Charger", slammed.transform.position + Vector3.right, target);
        struck.stunDuration = 0.5f;
        yield return null;
        struck.SwitchState(new ChargerWindupState(struck));
        struck.TryReceiveBeer(1f);
        var struckHealth = struck.currentHealth;
        slamHealth = slammed.currentHealth;
        Physics.SyncTransforms();
        slammed.TryPush(Vector3.right, 30f);
        deadline = Time.time + 2f;
        while (slammed.IsBeingPushed && Time.time < deadline) yield return null;
        Assert(slammed.currentHealth == slamHealth && struck.currentHealth == struckHealth,
            "Enemy collision damages neither patron");
        Assert(slammed.IsImpactStunned && struck.IsImpactStunned && struck.IsBeered,
            "Enemy collision stuns both patrons while preserving their beers");
        Assert(struck.TryTakeBackBeer(out _) && struck.IsImpactStunned,
            "Taking beer back during impact stun does not cancel the remaining stun");
        slammed.gameObject.SetActive(false); struck.gameObject.SetActive(false);

        foreach (var name in new[] { "Charger", "Haymaker", "Chair Thrower", "Powder Keg", "Piano Player" })
        {
            var enemy = Spawn<EnemyBrain>(name, centre + Vector3.up, target);
            enemy.enabled = false;
            enemy.mass = 50f; var heavyDistance = enemy.PushDistanceFor(30f);
            enemy.mass = 5f;
            Assert(Mathf.Approximately(enemy.PushDistanceFor(30f), heavyDistance * 10f), name + " inherits the same inverse-mass push response");
            enemy.gameObject.SetActive(false);
        }

        var prop = Prop(origin + Vector3.right * 110f, false);
        var slide = prop.gameObject.AddComponent<Pushable>();
        prop.Body.mass = 5f;
        Assert(slide.TryPush(Vector3.forward * 5f, 30f) && !prop.IsDangerous, "Furniture push does not arm impact damage");
        yield return new WaitForFixedUpdate(); yield return null;
        var owner = Spawn<ChairThrowerEnemy>("Chair Thrower", prop.transform.position + Vector3.right, target);
        owner.enabled = false;
        Assert(prop.TryHold(owner, owner.chairHoldPoint) && !slide.IsSliding, "Picking up a sliding prop cancels its floor-slide controller");
        prop.Release(Vector3.forward * 10f, 20f);
        yield return new WaitForFixedUpdate(); yield return null;
        Assert(!prop.Body.isKinematic && prop.Body.linearVelocity.z > 5f, "Floor-slide controller cannot freeze or overwrite a thrown prop");
        slide.TryPush(Vector3.forward, 30f);
        prop.ApplyBlast(Vector3.up * 12f);
        Assert(!slide.IsSliding, "Blast takes ownership from the floor-slide controller");
        prop.Body.isKinematic = true;
        Assert(prop.IsAvailableForPickup, "Resting kinematic Pushable furniture remains available to the thrower");
        prop.gameObject.SetActive(false); owner.gameObject.SetActive(false);

        var pushedProp = Prop(origin + Vector3.right * 160f, false);
        var pushedSlide = pushedProp.gameObject.AddComponent<Pushable>();
        var pushVictim = Spawn<ChargerEnemy>("Charger", pushedProp.transform.position + Vector3.forward * 1.2f, target);
        pushVictim.enabled = false;
        Physics.SyncTransforms();
        pushedSlide.TryPush(Vector3.forward, 30f);
        deadline = Time.time + 1f;
        while (!(pushVictim.CurrentState is EnemyStunState) && Time.time < deadline) yield return new WaitForFixedUpdate();
        yield return null;
        Assert(pushVictim.currentHealth == pushVictim.maxHealth && pushVictim.CurrentState is EnemyStunState,
            "Furniture slide stuns the enemy it hits without damaging it");
        pushedProp.gameObject.SetActive(false); pushVictim.gameObject.SetActive(false);

        var keg = Spawn<PowderKegEnemy>("Powder Keg", origin + Vector3.right * 140f, target);
        keg.target = keg.transform; keg.fuseSeconds = 10f;
        yield return null;
        var remaining = keg.FuseRemaining;
        keg.Stun();
        yield return new WaitForSeconds(0.2f);
        Assert(keg.FuseRemaining < remaining && keg.CurrentState is EnemyStunState,
            "Stunning a lit powder keg does not pause its fuse");
        keg.gameObject.SetActive(false);
    }

    private T Spawn<T>(string name, Vector3 position, Transform target) where T : EnemyBrain
    {
        var root = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Enemies/" + name + ".prefab"));
        created.Add(root);
        foreach (var detached in root.GetComponentsInChildren<DetachFurnitureOnStart>()) created.Add(detached.gameObject);
        var enemy = root.GetComponent<T>(); enemy.agent.enabled = false;
        root.transform.position = position; enemy.target = target;
        return enemy;
    }
    private ThrowableFurniture Prop(Vector3 position, bool gravity)
    {
        var root = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Combat/Pushable Stool.prefab"), position, Quaternion.identity);
        created.Add(root);
        var prop = root.GetComponent<ThrowableFurniture>();
        prop.Body.useGravity = gravity;
        return prop;
    }

    private void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("ENEMY CHECK FAILED: " + message);
        assertions++; Debug.Log("PASS: " + message);
    }
}
#endif
