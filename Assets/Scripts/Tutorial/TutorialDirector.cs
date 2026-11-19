using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runs the tutorial's lessons in order. Each lesson brings in its enemies and walks
/// the player through one mechanic using the prompter. Once its patrons are gone it
/// hands over to the level in the same scene, switching on the level's enemies.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [SerializeField] private TutorialPrompter prompter;

    [Header("Beering Lesson")]
    [Tooltip("Starts switched off. It comes in through the entrance once the lesson begins.")]
    [SerializeField] private ChargerEnemy charger;

    [Header("Pacing")]
    [Tooltip("Real seconds of quiet between one lesson ending and the next starting.")]
    [SerializeField, Min(0f)] private float lessonGapSeconds = 4f;
    [Tooltip("Real seconds of quiet before the closing line.")]
    [SerializeField, Min(0f)] private float endGapSeconds = 1.5f;

    [Header("Unbeering Lesson")]
    [Tooltip("Starts switched off. It comes in through the entrance once the lesson begins.")]
    [SerializeField] private HaymakerEnemy haymaker;
    [Tooltip("Starts switched off. Brought in as the Haymaker's target only if the Charger is already dead.")]
    [SerializeField] private EnemyBrain spareVictim;

    [Header("Ending")]
    [Tooltip("The tutorial's own Level Manager, which ends it once its patrons are gone. Held back until the last lesson, so losing the Charger early can't end the tutorial.")]
    [SerializeField] private LevelManager levelManager;

    [Header("Handing Over to the Level")]
    [Tooltip("Switched on once the tutorial is over, e.g. the level's enemies.")]
    [SerializeField] private GameObject[] enableWhenFinished;
    [Tooltip("The level's own Level Manager. Keep it switched off in the scene; it's switched on once the tutorial is over.")]
    [SerializeField] private LevelManager levelAfterTutorial;

    // Grace period for active practice. Reading cards always wait for the player.
    private const float MaxEndWaitSeconds = 5f;

    // Survives retries and menu visits, but never a new application/Editor play session.
    private static bool completedThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        completedThisSession = false;
    }

    private Coroutine lessons;
    private bool lessonsFinished;
    private bool diedDuringTutorial;
    private BeerInventory inventory;
    private PlayerHealth playerHealth;
    private LocomotionController player;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;

    private void OnDisable()
    {
        StopAllCoroutines();
        if (prompter) prompter.CancelFreeze();
        if (playerHealth) playerHealth.OnDied -= HandleTutorialDeath;
    }

    private void Start()
    {
        inventory = FindFirstObjectByType<BeerInventory>();

        // Mistakes are part of learning, so the tutorial never kills the player
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        // Where the level proper starts too: behind the bar
        player = FindFirstObjectByType<LocomotionController>();
        if (player) player.transform.GetPositionAndRotation(out playerStartPosition, out playerStartRotation);

        if (completedThisSession)
        {
            // These patrons belong only to the lessons, not the real fight.
            if (charger) charger.gameObject.SetActive(false);
            if (haymaker) haymaker.gameObject.SetActive(false);
            if (spareVictim) spareVictim.gameObject.SetActive(false);
            StartLevel();
            return;
        }

        if (playerHealth) playerHealth.OnDied += HandleTutorialDeath;
        if (playerHealth) playerHealth.CanDie = false;

        if (levelManager)
        {
            levelManager.enabled = false;
            levelManager.onLevelComplete.AddListener(EndTutorial);
        }

        lessons = StartCoroutine(RunLessons());
    }

    private IEnumerator RunLessons()
    {
        yield return BeeringLesson();
        yield return new WaitForSecondsRealtime(lessonGapSeconds);
        yield return UnbeeringLesson();

        if (levelManager)
        {
            levelManager.enabled = true;

            if (!levelManager.IsComplete)
            {
                yield return new WaitForSecondsRealtime(lessonGapSeconds);
                RestorePlayerHealth();
                prompter.SetLesson("PRACTICE  •  CLEAR THE ROOM");
                yield return prompter.Prompt(
                    "Keep it up: get them to take each other out.\nThe last one standing will run for the door.",
                    () => !levelManager || levelManager.IsComplete);
            }
        }

        lessonsFinished = true;
    }

    private void EndTutorial()
    {
        StartCoroutine(HandOverToLevel());
    }

    private void HandleTutorialDeath()
    {
        diedDuringTutorial = true;
        StopAllCoroutines();
        if (prompter) prompter.CancelFreeze();
    }

    private IEnumerator HandOverToLevel()
    {
        // Let the line on screen finish rather than cutting it off
        var deadline = Time.realtimeSinceStartup + MaxEndWaitSeconds;
        yield return new WaitUntil(() => lessonsFinished ||
            (!prompter.IsReading && Time.realtimeSinceStartup > deadline));

        if (lessons != null) StopCoroutine(lessons);
        prompter.CancelFreeze();
        prompter.Hide();
        prompter.HideHint();

        yield return new WaitForSecondsRealtime(endGapSeconds);

        prompter.SetLesson("TUTORIAL COMPLETE");
        yield return prompter.Explain("You're ready. Use beer to turn their attacks against each other.\nThe next fight starts when you continue.");

        if (diedDuringTutorial || (playerHealth && playerHealth.IsDead)) yield break;

        completedThisSession = true;
        StartLevel();
    }

    private void StartLevel()
    {
        if (prompter)
        {
            prompter.CancelFreeze();
            prompter.Hide();
            prompter.HideHint();
        }

        // The real fight starts fresh and for keeps
        if (playerHealth)
        {
            playerHealth.CanDie = true;
            playerHealth.RestoreFullHealth();
        }

        if (inventory) inventory.Refill();

        // Back behind the bar, facing the room, before the level's patrons come in
        if (player) player.ResetPositionRotation(playerStartPosition, playerStartRotation);

        // Takes the "Tutorial Complete" banner down with it
        if (levelManager) levelManager.gameObject.SetActive(false);

        foreach (var target in enableWhenFinished)
        {
            if (target) target.SetActive(true);
        }

        if (levelAfterTutorial) levelAfterTutorial.enabled = true;

        // Done: this also stops the tips and anything else the tutorial was running
        gameObject.SetActive(false);
    }

    private IEnumerator BeeringLesson()
    {
        if (!charger || charger.IsDead) yield break;

        prompter.SetLesson("1 / 2  •  STOP THE CHARGE");
        yield return prompter.Explain("Hand an attacking enemy a beer to freeze his attack.\nGet close, aim at him, then press {0}.", InputHandler.PromptAction.Beer);

        charger.gameObject.SetActive(true);

        var player = playerHealth;

        // Repeats until the player gets out of the way of the charge
        while (true)
        {
            // The wind-up is the opening for a parry
            yield return new WaitUntil(() => !charger || charger.IsDead || charger.CanBeBeered);
            if (!charger || charger.IsDead) yield break;

            var windupPosition = charger.transform.position;

            yield return prompter.FreezePrompt(
                "Stop the Charger.\nAim at him and press {0} to hand him a beer.",
                () => !charger || charger.IsDead || charger.IsBeered, () => charger && charger.CanBeBeered && CanAimAt(charger), InputHandler.PromptAction.Beer);
            if (!charger || charger.IsDead) yield break;

            yield return prompter.Explain("Beer pauses his attack — it does not cancel it.\nMove to the side before he finishes his drink.");

            var playerHealthBefore = player ? player.CurrentHealth : 0f;
            var chargerHealthBefore = charger.currentHealth;

            yield return prompter.Prompt(
                "Step out of his path.\nHe will charge straight ahead when his drink runs out.",
                () => !charger || charger.IsDead || charger.CurrentState is ChargerRecoverState);
            if (!charger || charger.IsDead) yield break;

            if (player && player.CurrentHealth < playerHealthBefore)
            {
                // Holds him still while the player reads, then sends him back for another go
                charger.StopPathfinding();
                charger.enabled = false;

                yield return prompter.Explain("The charge caught you. Try stepping sideways after giving him beer.\nYour health and beer will be restored for another try.");
                if (!charger || charger.IsDead) yield break;

                ResetCharger(windupPosition);
                continue;
            }

            yield return prompter.Explain(charger.currentHealth < chargerHealthBefore
                    ? "Good dodge! He hit the bar.\nYou can use his charge against walls and other enemies."
                    : "Good dodge! You stopped his attack and moved out of danger.");
            yield break;
        }
    }

    private void ResetCharger(Vector3 windupPosition)
    {
        if (NavMesh.SamplePosition(windupPosition, out var hit, 2f, NavMesh.AllAreas))
        {
            charger.agent.Warp(hit.position);
        }

        charger.enabled = true;
        charger.ReturnToDefaultState();

        // A glass is lost on every attempt, so top the player back up rather than leave them empty-handed
        if (inventory) inventory.Refill();
        RestorePlayerHealth();
    }

    // Each lesson and retry starts the player fresh, rather than limping on at 1 health
    private void RestorePlayerHealth()
    {
        if (playerHealth) playerHealth.RestoreFullHealth();
    }

    private IEnumerator UnbeeringLesson()
    {
        if (!haymaker || haymaker.IsDead) yield break;

        RestorePlayerHealth();
        prompter.SetLesson("2 / 2  •  TURN THE PUNCH AGAINST THEM");
        yield return prompter.Explain("The Haymaker punches everyone in front of him.\nFirst, get close and give him a beer during his wind-up {0}.", InputHandler.PromptAction.Beer);

        haymaker.gameObject.SetActive(true);

        // The lesson needs someone for him to punch
        if ((!charger || charger.IsDead) && spareVictim) spareVictim.gameObject.SetActive(true);

        // Everyone the tutorial uses is in now, so losing a patron can end it
        if (levelManager) levelManager.enabled = true;

        EnemyBrain victim = null;

        // Repeats until the player takes the beer back with another patron in the swing
        while (!victim)
        {
            if (inventory && !inventory.HasBeer)
            {
                yield return prompter.Prompt("You're out of beer! Refill at the tap behind the bar {0}",
                    () => !inventory || inventory.HasBeer, InputHandler.PromptAction.Interact);
            }

            yield return new WaitUntil(() => !haymaker || haymaker.IsDead || haymaker.CanBeBeered);
            if (!haymaker || haymaker.IsDead) yield break;

            // His wind-up is short, so this freezes too. Missing it lets the swing play out.
            yield return prompter.FreezePrompt(
                "Stop the Haymaker's punch.\nAim at him and press {0} to hand him a beer.",
                () => !haymaker || haymaker.IsDead || haymaker.IsBeered || !haymaker.CanBeBeered || (inventory && !inventory.HasBeer), () => haymaker && haymaker.CanBeBeered && CanAimAt(haymaker), InputHandler.PromptAction.Beer);
            if (!haymaker || haymaker.IsDead) yield break;
            if (!haymaker.IsBeered) continue;

            yield return prompter.Explain("Put another enemy in front of his raised fist.\nLure one over, or push a beered enemy with {0}.", InputHandler.PromptAction.Push);
            yield return prompter.Prompt(
                "Line up another enemy in front of the Haymaker.\nHe will finish his punch when the beer is gone.",
                () => !haymaker || haymaker.IsDead || !haymaker.IsBeered || FindPatronInSwing());
            if (!haymaker || haymaker.IsDead) yield break;

            victim = FindPatronInSwing();
            if (!victim)
            {
                yield return prompter.Explain("His drink ran out before anyone was in position.\nGive him another beer and try lining up an enemy sooner.");
            }
        }

        yield return prompter.Explain("An enemy is in his path. Take the beer back to release the punch.\nGet close, aim at the Haymaker and press {0}.", InputHandler.PromptAction.Unbeer);

        // Read before resuming: the punch can land while the game is still speeding back up
        var victimHealthBefore = victim.currentHealth;

        yield return prompter.FreezePrompt(
            "Release the punch.\nAim at the Haymaker and press {0} to take your beer back.",
            () => !haymaker || haymaker.IsDead || !haymaker.IsBeered, () => CanAimAt(haymaker), InputHandler.PromptAction.Unbeer);

        // Wait for the punch to finish
        yield return new WaitUntil(() => !haymaker || haymaker.IsDead || !haymaker.CanBeBeered);

        var landed = !victim || victim.IsDead || victim.currentHealth < victimHealthBefore;

        yield return prompter.Explain(landed
                ? "It worked! His punch hit the other enemy.\nUse beer to control when enemies attack."
                : "The other enemy moved out of the swing.\nNext time, take the beer back while they are still in front of him.");
    }

    private bool CanAimAt(EnemyBrain enemy)
    {
        var aim = player ? player.GetComponent<PlayerAim>() : null;
        return aim && enemy && aim.FindAimed<EnemyBrain>() == enemy;
    }

    private EnemyBrain FindPatronInSwing()
    {
        if (!haymaker || !haymaker.IsBeered) return null;

        foreach (var enemy in EnemyBrain.All)
        {
            if (haymaker.IsInSwing(enemy)) return enemy;
        }

        return null;
    }

}
