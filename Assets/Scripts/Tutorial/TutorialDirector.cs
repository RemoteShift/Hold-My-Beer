using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the tutorial's lessons in order. Each lesson brings in its enemies and walks
/// the player through one mechanic using the prompter.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [SerializeField] private TutorialPrompter prompter;

    [Header("Beering Lesson")]
    [Tooltip("Starts switched off. It comes in through the entrance once the lesson begins.")]
    [SerializeField] private ChargerEnemy charger;
    [Tooltip("Real seconds the opening line stays up before the Charger comes in.")]
    [SerializeField, Min(0f)] private float introSeconds = 3f;
    [Tooltip("Real seconds a closing line stays up after a lesson.")]
    [SerializeField, Min(0f)] private float outroSeconds = 3f;

    [Header("Unbeering Lesson")]
    [Tooltip("Starts switched off. It comes in through the entrance once the lesson begins.")]
    [SerializeField] private HaymakerEnemy haymaker;
    [Tooltip("Starts switched off. Brought in as the Haymaker's target only if the Charger is already dead.")]
    [SerializeField] private EnemyBrain spareVictim;

    [Header("Ending")]
    [Tooltip("Held back until the last lesson, so losing the Charger early can't end the tutorial.")]
    [SerializeField] private LevelManager levelManager;
    [Tooltip("Scene loaded once the tutorial is over. It must be in the Build Settings.")]
    [SerializeField] private string nextScene = "Level 1";
    [Tooltip("Real seconds the closing line stays up before the next scene loads.")]
    [SerializeField, Min(0f)] private float endSeconds = 5f;

    private Coroutine lessons;
    private BeerInventory inventory;
    private InputHandler input;

    // Set during a freezing prompt. A frozen player can't walk into reach or step around
    // anyone in the way, so the press works on this enemy from wherever they are.
    private EnemyBrain beerTarget;
    private EnemyBrain takeBackTarget;
    private bool beerPressed;
    private bool interactPressed;

    private void OnEnable()
    {
        input = InputHandler.Instance;
        if (!input) return;

        input.OnBeerPressed += HandleBeerPressed;
        input.OnInteractPressed += HandleInteractPressed;
    }

    private void OnDisable()
    {
        if (!input) return;

        input.OnBeerPressed -= HandleBeerPressed;
        input.OnInteractPressed -= HandleInteractPressed;
    }

    private void Start()
    {
        inventory = FindFirstObjectByType<BeerInventory>();

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
        yield return UnbeeringLesson();

        if (!levelManager) yield break;

        levelManager.enabled = true;

        yield return prompter.Prompt(
            "Keep it up: get them to take each other out.\nThe last one standing will run for the door.",
            () => !levelManager || levelManager.IsComplete);
    }

    private void EndTutorial()
    {
        if (lessons != null) StopCoroutine(lessons);
        prompter.CancelFreeze();
        prompter.HideHint();

        StartCoroutine(LoadNextScene());
    }

    private IEnumerator LoadNextScene()
    {
        prompter.Show("That's the basics! Now go get your hat back.");
        yield return new WaitForSecondsRealtime(endSeconds);

        // The player and HUD are kept between scenes, but the next scene brings its own
        var player = FindFirstObjectByType<LocomotionController>();
        if (player) Destroy(player.transform.root.gameObject);

        SceneManager.LoadScene(nextScene);
    }

    private IEnumerator BeeringLesson()
    {
        if (!charger) yield break;

        yield return ShowFor("Somebody's not happy with the service...", introSeconds);

        charger.gameObject.SetActive(true);

        // The wind-up is the opening for a parry
        yield return new WaitUntil(() => !charger || charger.CanBeBeered);
        if (!charger) yield break;

        beerTarget = charger;
        yield return prompter.FreezePrompt(
            "He's about to charge!\nLook at him and press [LMB] to hand him a beer",
            () => !charger || charger.IsBeered);
        beerTarget = null;
        if (!charger) yield break;

        var player = FindFirstObjectByType<PlayerHealth>();
        var playerHealthBefore = player ? player.CurrentHealth : 0f;
        var chargerHealthBefore = charger.currentHealth;

        yield return prompter.Prompt(
            "He's frozen until he finishes his drink, then he'll carry on charging.\nGet out of his way!",
            () => !charger || charger.CurrentState is ChargerRecoverState);

        string outro;
        if (player && player.CurrentHealth < playerHealthBefore)
            outro = "Too slow! Step aside next time and let him hit something else.";
        else if (!charger || charger.currentHealth < chargerHealthBefore)
            outro = "He ran into the bar instead of you. Walls hurt!";
        else
            outro = "Nice dodge!";

        yield return ShowFor(outro, outroSeconds);
    }

    private IEnumerator UnbeeringLesson()
    {
        if (!haymaker) yield break;

        yield return ShowFor("Here comes the Haymaker. His punch hits everyone in front of him.", introSeconds);

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
                yield return prompter.Prompt("You're out of beer! Refill at the tap behind the bar [E]",
                    () => !inventory || inventory.HasBeer);
            }

            yield return new WaitUntil(() => !haymaker || haymaker.CanBeBeered);
            if (!haymaker) yield break;

            // His wind-up is short, so this freezes too. Missing it lets the swing play out.
            beerTarget = haymaker;
            yield return prompter.FreezePrompt(
                "He's winding up!\nPress [LMB] to beer him",
                () => !haymaker || haymaker.IsBeered || !haymaker.CanBeBeered || (inventory && !inventory.HasBeer));
            beerTarget = null;
            if (!haymaker) yield break;
            if (!haymaker.IsBeered) continue;

            yield return prompter.Prompt(
                "Once his drink is gone, he'll finish his punch.\nGet another patron in front of him: lure them over, or beer them and push them [RMB]",
                () => !haymaker || !haymaker.IsBeered || FindPatronInSwing());
            if (!haymaker) yield break;

            victim = FindPatronInSwing();
            if (!victim)
            {
                yield return ShowFor("He finished his drink. Try again!", outroSeconds);
            }
        }

        // Read before resuming: the punch can land while the game is still speeding back up
        var victimHealthBefore = victim.currentHealth;

        takeBackTarget = haymaker;
        yield return prompter.FreezePrompt(
            "Someone's in front of him, and he finishes his punch once his beer's gone.\nPress [E] to take your beer back!",
            () => !haymaker || !haymaker.IsBeered);
        takeBackTarget = null;

        // Wait for the punch to finish
        yield return new WaitUntil(() => !haymaker || haymaker.IsDead || !haymaker.CanBeBeered);

        var landed = !victim || victim.IsDead || victim.currentHealth < victimHealthBefore;

        yield return ShowFor(landed
                ? "Ouch! Let them do the fighting for you."
                : "He missed! Take the beer back while they're still in front of him.",
            outroSeconds);
    }

    private void HandleBeerPressed() => beerPressed = true;
    private void HandleInteractPressed() => interactPressed = true;

    // Runs after the player's own handlers have had the press, so a press that already
    // worked on the target isn't repeated
    private void Update()
    {
        if (beerPressed && beerTarget && beerTarget.CanBeBeered && !beerTarget.IsBeered &&
            inventory && inventory.TryTakeRightmost(out var fill))
        {
            beerTarget.TryReceiveBeer(fill);
        }

        if (interactPressed && takeBackTarget && takeBackTarget.TryTakeBackBeer(out var kept) && inventory)
        {
            inventory.TryAddGlass(kept);
        }

        beerPressed = false;
        interactPressed = false;
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

    private IEnumerator ShowFor(string text, float seconds)
    {
        prompter.Show(text);
        yield return new WaitForSecondsRealtime(seconds);
        prompter.Hide();
    }
}
