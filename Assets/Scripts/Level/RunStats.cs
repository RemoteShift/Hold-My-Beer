using UnityEngine;

/// <summary>
/// Keeps score of the current run for the game over screen: beers handed to enemies,
/// enemies remaining, refills used and time survived. Counting starts once the level itself is underway
/// (after any tutorial) and stops at <see cref="End"/>.
/// </summary>
public class RunStats : MonoBehaviour
{
    [Tooltip("Counting starts once this has started gameplay. Leave empty to start when the scene loads.")]
    [SerializeField] private GameplayStarter starter;

    [Tooltip("The level's own Level Manager. Counting also waits for it to be switched on, so a tutorial played first doesn't count. Leave empty to start with gameplay.")]
    [SerializeField] private LevelManager level;

    /// <summary>Beers handed over, counting every hand-off even to the same enemy.</summary>
    public int EnemiesBeered { get; private set; }
    public int RefillsUsed { get; private set; }

    /// <summary>Game time since gameplay started, so pauses and freezes don't count.</summary>
    public float SecondsSurvived => hasBegun ? (hasEnded ? endTime : Time.time) - startTime : 0f;

    /// <summary>Enemies still in the fight: the live count, or the count when the run ended.</summary>
    public int EnemiesRemaining => hasEnded ? enemiesRemainingAtEnd : CountEnemiesRemaining();

    public bool IsCounting => hasBegun && !hasEnded;

    private BeerHandler beerHandler;
    private bool hasBegun;
    private bool hasEnded;
    private float startTime;
    private float endTime;
    private int enemiesRemainingAtEnd;

    private void Update()
    {
        if (!hasBegun && (!starter || starter.HasStarted) && (!level || level.isActiveAndEnabled)) Begin();
    }

    private void OnDestroy()
    {
        if (!beerHandler) return;

        beerHandler.OnBeerHanded -= CountBeer;
        beerHandler.OnRefilled -= CountRefill;
    }

    private void Begin()
    {
        hasBegun = true;
        startTime = Time.time;

        // The player only exists once gameplay has started
        beerHandler = FindFirstObjectByType<BeerHandler>();
        if (!beerHandler) return;

        beerHandler.OnBeerHanded += CountBeer;
        beerHandler.OnRefilled += CountRefill;
    }

    /// <summary>Stops counting, freezing the stats as they are, e.g. when the player dies.</summary>
    public void End()
    {
        if (!IsCounting) return;

        hasEnded = true;
        endTime = Time.time;
        enemiesRemainingAtEnd = CountEnemiesRemaining();
    }

    // Switched-off enemies aren't part of the fight, and dying ones are already out of it
    private static int CountEnemiesRemaining()
    {
        var count = 0;

        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
        {
            if (!enemy.IsDead) count++;
        }

        return count;
    }

    private void CountBeer()
    {
        if (IsCounting) EnemiesBeered++;
    }

    private void CountRefill()
    {
        if (IsCounting) RefillsUsed++;
    }
}
