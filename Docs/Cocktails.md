# Ingredients and cocktails

The persistent player prefab includes the ingredient inventory, brewer, drink effects, aiming preview and HUD. Older scene copies also acquire the kit in `BeerHandler.Awake`. Ingredient counts start at zero, survive scene transitions with the existing persistent player, and reset when retry/menu destroys that player.

## Controls

| Hold key | Cocktail |
| --- | --- |
| 1 | Honey Brew |
| 2 | Fizzy Soda Splash |
| 3 | Aggression Tonic |
| 4 | All-You-Can-Drink |
| 5 | Steel-Gut Vodka |

Hold one number key to preview its cocktail. If several are held, the most recently pressed key wins. Releasing it falls back to the most recently pressed key still held. With none held, the selection window is empty.

Hold a number key to select a cocktail and preview its trajectory and landing radius. Press LMB to immediately brew and throw, or drink. Holding LMB does not repeat the action; release and click again for another cocktail. RMB does not cancel brewing. An unaffordable click displays a red **X** and spends nothing. Releasing all cocktail keys hides the selection panel and aiming preview. Ingredient inventory remains visible. A brewing click cannot also hand over/take back beer or push something.

**E** handles push and refill again. LMB/RMB retain normal beer/unbeer behavior when no cocktail is selected. Q/E/Z/C ingredient selection and the Tab recipe panel have been removed. The cocktail HUD consists of four inventory rows with icons, names and quantities plus one small dynamic window with the cocktail icon/name and ingredient icons/costs. No ingredient-key badges or selection highlights remain. Pickups are collected by walking within 1.8m with a clear path.

## Recipes

| Cocktail | Keys | Cost | Effect |
| --- | --- | --- | --- |
| Honey Brew | 1 | 1 Life Essence + 2 Sugar | Throwable 3m floor patch lasting 5s. Each entrant receives its normal stun, then returns to default AI while rooted for 5s. After the stun it can track and attack but cannot move. Existing beer freezes and lit fuse rules remain. |
| Fizzy Soda Splash | 2 | 2 Water + 1 Sugar | 3m area knockback away from impact, with full strength throughout the radius and no damage. Existing mass response and honey immunity still apply. |
| Aggression Tonic | 3 | 1 Water + 2 Gunpowder | 4m splash redirects affected patrons toward other living patrons for 2s. Previous targets return afterward. |
| All-You-Can-Drink | 4 | 3 Life Essence + 1 carried Beer | Unlimited full servings for 10s. The beer ingredient is automatic. An active buff cannot fund or refresh itself. |
| Steel-Gut Vodka | 5 | 2 Life Essence + 1 Gunpowder | Ignores incoming attack damage for 6s. |

Costs and durations follow the supplied GDD. The GDD does not specify radii, flight speed or knockback strength; the initial balance uses Honey 3m, Aggression 4m, launch speed 16m/s with 2.5m/s upward lift, and soda push strength 65. Select `Assets/Resources/CocktailSettings.asset` in the Project window to edit all cocktail balance parameters in the Inspector. Expand a named recipe for its display name, color, radius, duration and individual ingredient costs. Shared bottle flight settings and soda push strength are below the recipes. Change values outside Play mode for deliberate persistent balance edits. Preview, brewing, HUD and actual projectiles all read this shared asset. Recipe identities, keys and drink/throw behavior remain fixed.

Preview and actual projectile use the same gravity curve and swept sphere collisions, ignoring the owner. Aim arcs stop at the first obstacle; area circles project onto the supporting floor. Bottles also leave a trail in flight. Splash effects respect walls and floor height. Buff timers use game time; death clears drink buffs, and disabling controls, death, loss of focus or pausing cancels a pending throw with a refund.

## Enemy drops

Each optional unit is rolled **independently**. A later unit can drop even if an earlier optional unit failed. Guaranteed units require no random roll. Drops occur once on death, including powder-keg chain deaths; nonlethal departure/despawn does not award death loot.

| Enemy | Per-unit drops |
| --- | --- |
| Charger | Life Essence: 100%; Water: 60% |
| Haymaker | Life Essence: 100%, 50%, 20%; Water: 60% |
| Chair Thrower | Sugar: 100%, 60%; Life Essence: 100% |
| Powder Keg | Gunpowder: 70%, 50%; Life Essence: 100% |
| Piano Player | Sugar: 25%; Life Essence: 100%; Gunpowder: 25%; Water: 25% |

`EnemyIngredientDrops.GetTable` exposes the tables and `Roll` accepts an injected random function for exact boundary checks. Pickups use procedural colored shapes and labels; bottles, floor splashes and trajectory lines use the included URP `Resources/CocktailVisual.shader`.

## Validation

The **Hold My Beer** editor menu contains four Play-mode suites: Cocktail Inventory Checks, Cocktail Enemy Checks, Cocktail Projectile Checks and Cocktail Input Checks. Inventory/enemy/projectile suites create isolated fixtures. Input Checks requires active, unpaused gameplay outside a tutorial prompt, adds temporary input devices, temporarily sets ingredient counts and restores the original inventory afterward. It captures idle, pre-click preview, failure and aiming states to `/tmp/hmb-cocktail-*.png`.

Checks cover number-key precedence and fallback, empty selection, all five shortcuts, confirmation locking, insufficient-ingredient feedback, costs and refunds, drink expiry, E push/refill, projectile release/cancel and pickup behavior. Enemy and projectile suites cover the original drop boundaries, status effects and ballistic collision behavior.

The pre-existing `PC_Renderer is missing RendererFeatures` editor error remains outside this change.

Direct-selection revision verified in Unity 6000.3.11f1 on 2026-11-17: 43 inventory/selection assertions and 44 real-input/HUD assertions passed. Idle, pre-click selection, insufficient-ingredient X and confirmed aiming states were visually inspected at 1920x1080. All visible ingredient and cocktail icons have rendered vector geometry. `git diff --check` passes. Unity was left out of Play mode with the test inventory restored.

Aggression Tonic redirects a pianist's music to the player within its music radius for the tonic duration; it keeps playing instead of attacking. Movement speed, push recovery, defense and maximum-health buffs apply to the player (health percentage is preserved when the maximum changes). Enemy-only attack windup, attack speed, damage and AI turning channels have no corresponding player attack and do not alter mouse sensitivity or bottle trajectories. Normal enemy music benefits resume when the tonic expires. Buff strengths and toggles remain on the Piano Player prefab's Music Buffs settings.

Throwable landing circles persist during flight. Impact circles keep their full radius: Aggression lasts for its configured duration, soda remains while affected enemies slide (minimum 0.3s), and Honey remains until late entrants finish their roots. The Honey patch stops accepting new entrants at its configured expiry. Soda uses its Inspector radius and shared soda push strength; walls and floor separation still block area effects.
