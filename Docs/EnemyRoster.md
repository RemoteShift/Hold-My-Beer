# Enemy roster and project map

## Try it

Open `Assets/_Testing/Scenes/Enemy Sandbox.unity` and press Play. This is a copy of Level 1 with the original chargers, all four new enemies, a falling chandelier, and throwable chairs/stools. The original Level 1 is unchanged.

Existing controls remain: LMB hands over beer during an attack; E takes it back or refills at a beer source. RMB, F, or gamepad west shoves the aimed patron or furniture. Shoving a frozen patron preserves their facing. Aim at the chandelier and shove to release it.

Prefabs live in `Assets/_Game/Prefabs/Enemies/`; reusable furniture is in `Assets/_Game/Prefabs/Combat/`. Drag enemy prefabs onto a baked NavMesh. Each has the existing health and beer timer UI and held-glass references. Add `ShoveHandler` to a player in other scenes, and `ThrowableFurniture` to chairs/stools with rigidbodies and colliders. Mark fixed hazards `canBePickedUp = false`.

## Gameplay

- **Haymaker:** slow approach, yellow windup, then a 150-degree sweep. The visible bare fist follows the sweep. Each actor can be hit once, including other enemies. Beer preserves both pose and swing progress; after unbeer the facing stays locked. Default health 120, damage 35.
- **Chair Thrower:** starts visibly holding a real chair overhead. Charges for 1.5 seconds, then throws it on a ballistic trajectory. Beer pauses charging and locks the aim; moving the target afterwards does not redirect it. The chair becomes physical on release, damages patrons or Cassidy, and remains usable furniture. The thrower retrieves nearby available chairs/stools after recovery, rather than conjuring replacement ammunition. Default health 60, damage 30.
- **Powder Keg:** lights a four-second fuse when Cassidy is detected and sprints towards them. A shortening chest fuse and flickering light indicate the countdown. Beer pauses the fuse exactly; manual unbeer or an empty glass resumes the remaining time. Explosion damages nearby actors, including enemies, and pushes rigidbodies. Solid scenery blocks blast damage. Default health 50, blast damage 80, radius 5. Lethal damage also detonates it, even while beered. Chain reactions are allowed; each keg explodes only once. Loose furniture receives a mass-independent outward/upward velocity change, with strength falling to zero at the blast radius. Tune `Furniture Blast Speed`, `Furniture Blast Lift`, and `Furniture Blast Falloff`.
- **Piano Player:** stationary seated support with moving hands and a spatial looping piano cue. Enemies within 12 units receive whichever independent buffs are enabled in the Inspector. Movement, windup, attack execution, and recovery default to ×1.35; turning, damage, defense, and HP default off. Beer immediately pauses music, hands, and the buff; unbeer resumes them. Death/disabling/range exit removes the bonus; multiple pianists use the strongest enabled bonus per stat instead of multiplying. Fuse duration and beer consumption remain real-time. Default health 30, so one sufficiently fast stool impact can defeat it.

These are playable prototype visuals, matching the existing capsule charger: primitive fists/arms and seated musician/piano, existing Poly Universal Pack chair/stool/dynamite/chandelier models, and an original synthesized placeholder piano loop. Final character rigs, animation clips, combat audio, explosion effects, and balance polish remain art/design work.

## Code map

The project's first-party gameplay code is under `Assets/Scripts`:

- `Input/InputHandler` reads Input System action references. `Movement/LocomotionController`, grounded/airborne states, ground contact tracking, and `CameraLook` provide Rigidbody-based first-person movement, sprinting, jumping, and aiming.
- `Player/BeerInventory` stores each glass's fullness. `BeerHandler` sphere-casts from the camera and hands beer to attacking enemies, retrieves it, or refills at `Interactables/BeerSource`. UI scripts show health, inventory, context prompts, and freeze duration.
- `Enemies/EnemyBrain` owns health, NavMesh movement, targeting, beer interaction, and state transitions. `EnemyBeeredState` holds the interrupted state object, consumes fullness, and resumes that same object. New archetypes use this existing mechanism; their timers advance only when their active state updates.
- Charger chase/windup/charge/recovery states remain in `States/Enemy/Charger`. The four new enemies keep their compact archetype states inside their respective scripts. Separate `WindupDeltaTime`, `AttackDeltaTime`, and `RecoveryDeltaTime` apply the corresponding pianist bonuses without altering beer/fuse timers. `EnemyBrain` owns shared `moveSpeed`, `turnSpeed`, `damage`, `defense`, and health values. Navigation reads the brain's move speed each frame; tuning only NavMeshAgent.speed is no longer the source of truth.
- `Combat/CombatQuery` handles shared blast damage deduplication and scenery occlusion. `ThrowableFurniture` owns pickup, physical release, shoving, and collision damage. `FallingChandelier` releases a suspended furniture body when damaged or shoved. `DetachFurnitureOnStart` makes the packaged piano stool independent in play.
- `Editor/EnemyRosterBuilder` provides **Hold My Beer > Build Enemy Prefabs** and **Create Enemy Sandbox From Current Level**. These rebuild generated assets / overwrite the sandbox; edit prefab art directly only if you do not intend to rebuild it.

Third-party content is under `Assets/Plugins` (Poly Universal Pack, NaughtyAttributes, DOTween, inspector tools, sky assets) and Unity packages. Level 1 and Football are testing scenes; Persistents supplies the player/input setup. No vendor code was changed.

## Verification

In Play mode, run **Hold My Beer > Run Enemy Behaviour Checks (Play Mode)**. The test harness uses the actual prefabs in an isolated region, checks their gameplay interactions, cleans up test objects, and restores the encounter enemies. Verified in Unity 6000.3.11f1: all 66 assertions passed. The checks cover locked attacks, automatic beer expiry, paused fuses, death explosions, blast occlusion and furniture falloff, held grip alignment, independent buffs, HP/defense behavior, furniture disarming/rearming, a chandelier defeating the pianist, first-step high-speed impacts, multi-collider hit deduplication, stationary-target charges at NavMesh edges, and autonomous pickup of low stools and offset-pivot furniture. Results appear as `PASS` lines and a final assertion count in the Console. Stop Play to restore the initial encounter.

## Scene view gizmos

Enable **Gizmos** in the Scene view and select an enemy (or one of its children). Ranges are labeled in world-space meters and update with Inspector values. Green shows detection; yellow shows attack initiation; red shows damage arcs, blast radius, charge corridors, and throw trajectories; cyan shows furniture search/pickup; purple shows the music aura. The pianist's aura turns gray when inactive in Play mode. A white arrow shows facing, and labels show beer/fuse status. Throw previews use the actual stored attack aim while charging or beered; the ballistic curve is an unobstructed prediction, not a collision simulation. The charge corridor shows configured maximum travel, not remaining travel. Gizmo rendering lives entirely in `Assets/Editor/EnemyRangeGizmos.cs`.


## Charger attack initiation

The charger checks physical visibility and horizontal range to enter windup. The player does not have to stand on the baked NavMesh; charge movement still respects navigation boundaries. While chasing, stopping distance is capped inside attack range so tuning a short attack range cannot leave it parked outside the attack trigger.

## Furniture pickup and damage

`Pickup Radius` is the search radius: the thrower can notice furniture this far away and walk towards it. `Pickup Reach` is the 3D distance from the enemy centre to the nearest furniture collider surface needed to grab it. Both measurements use physical collider geometry rather than model pivots. Furniture retrieval temporarily overrides combat stopping distance, stops as soon as a grab succeeds, then restores the normal distance. Props whose reachable navigation endpoint is still out of reach or behind solid scenery are skipped.

Held furniture disables physical colliders/interpolation, aligns its optional `Grip Point` with `Chair Hold Point`, and maintains that pose after physics updates. Without a grip, the visual bounds centre aligns with the hold point and the item's root rotation matches it. Use per-item `Held Position Offset` / `Held Rotation Offset` to adjust a particular prop. A grip is best for models with unusual pivots or authored rotations.

A throw, shove, released chandelier, or explosion arms furniture. While armed and moving fast enough, it hurts Cassidy and other enemies once per actor. Any collision with a non-damageable object (including floors, walls, and ordinary furniture) immediately disarms it—even a slow collision. Continued contact with a floor also disarms it. Merely gaining speed again does not re-arm it; a new throw/shove/blast/drop does. Arming also enables continuous collision detection for ordinary scene stools, and shove/blast impulses count toward impact speed on the first simulation step. Impact damage uses pre-simulation speed, because continuous-collision callbacks can report zero relative velocity after stopping the object. Thrown damage is snapshotted on release; subsequent shoves use the item's own `Impact Damage`.

## Piano buff controls

Every row under **Music Buffs** has its own checkbox and multiplier. Disabled effects use ×1. Values update live in Play mode.

| Buff | Changes |
| --- | --- |
| Move Speed | Normal NavMesh pursuit, furniture retrieval, powder-keg sprint |
| Turn Speed | Facing/aim interpolation |
| Windup Speed | Charger and Haymaker windup, chair throw charge time |
| Attack Speed | Charger dash speed, Haymaker swing speed, chair projectile speed (shorter flight time at launch) |
| Recovery Speed | Post-attack cooldown; ×2 means half the recovery time |
| Damage | Outgoing melee, charge, thrown-chair, and blast damage |
| Defense | Incoming damage divisor; ×2 means half damage taken |
| Max Health | Effective maximum HP; both current/max HP scale together when added or removed |

HP percentage is preserved across aura changes, so entering/exiting or toggling HP cannot heal existing wounds. Defense and HP are separate effects. Beer/fuse countdowns and the music playback rate are never sped up by these settings. Existing project tuning was migrated to the shared stats; rebuilding prefabs from the menu deliberately restores builder defaults.
