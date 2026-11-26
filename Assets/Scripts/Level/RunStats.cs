using UnityEngine;

/// <summary>
/// Keeps score of the current run for the game over screen: beers handed to enemies,
/// enemies remaining, refills used and time survived. Counting starts with gameplay and stops at <see cref="End"/>.
/// </summary>
public class RunStats : MonoBehaviour
{
    [Tooltip("Counting starts once this has started gameplay. Leave empty to start when the scene loads.")]
    [SerializeField] private GameplayStarter starter;

    /// <summary>Beers handed over, counting every hand-off even to the same enemy.</summary>
    public int EnemiesBeered { get; private set; }
    public int RefillsUsed { get; private set; }
    public int CocktailsMixed { get; private set; }
    public int BruisesEarned { get; private set; }
    public float DamageTaken { get; private set; }

    /// <summary>Game time since gameplay started, so pauses and freezes don't count.</summary>
    public float SecondsSurvived => hasBegun ? (hasEnded ? endTime : Time.time) - startTime : 0f;

    /// <summary>Enemies still in the fight: the live count, or the count when the run ended.</summary>
    public int EnemiesRemaining => hasEnded ? enemiesRemainingAtEnd : CountEnemiesRemaining();

    public bool IsCounting => hasBegun && !hasEnded;

    private BeerHandler beerHandler;
    private CocktailBrewer cocktailBrewer;
    private PlayerHealth playerHealth;
    private bool hasBegun;
    private bool hasEnded;
    private float startTime;
    private float endTime;
    private int enemiesRemainingAtEnd;

    private void Update()
    {
        if (!hasBegun && (!starter || starter.HasStarted)) Begin();
        if (IsCounting) HookPlayer();
    }

    private void OnDestroy()
    {
        if (beerHandler)
        {
            beerHandler.OnBeerHanded -= CountBeer;
            beerHandler.OnRefilled -= CountRefill;
        }
        if (cocktailBrewer) cocktailBrewer.OnMixed -= CountCocktail;
        if (playerHealth) playerHealth.OnDamageTaken -= CountDamage;
    }

    private void Begin()
    {
        hasBegun = true;
        startTime = Time.time;

        // The player only exists once gameplay has started
        HookPlayer();
    }

    private void HookPlayer()
    {
        if (!beerHandler)
        {
            beerHandler = FindFirstObjectByType<BeerHandler>();
            if (beerHandler) { beerHandler.OnBeerHanded += CountBeer; beerHandler.OnRefilled += CountRefill; }
        }
        if (!cocktailBrewer)
        {
            cocktailBrewer = FindFirstObjectByType<CocktailBrewer>();
            if (cocktailBrewer) cocktailBrewer.OnMixed += CountCocktail;
        }
        if (!playerHealth)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth) playerHealth.OnDamageTaken += CountDamage;
        }
    }

    /// <summary>The real fight starts fresh after the tutorial.</summary>
    public void ResetForLevel()
    {
        EnemiesBeered = RefillsUsed = CocktailsMixed = BruisesEarned = 0;
        DamageTaken = 0f;
        hasBegun = true; hasEnded = false;
        startTime = Time.time;
        HookPlayer();
    }

    private void CountCocktail() { if (IsCounting) CocktailsMixed++; }
    private void CountDamage(float amount)
    {
        if (!IsCounting || amount <= 0f) return;
        BruisesEarned++;
        DamageTaken += amount;
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
