using UnityEngine;

/// <summary>
/// Keeps score of the current run for the game over screen: beers handed to enemies,
/// refills used and time survived. Counting starts with gameplay and stops at <see cref="End"/>.
/// </summary>
public class RunStats : MonoBehaviour
{
    [Tooltip("Counting starts once this has started gameplay. Leave empty to start when the scene loads.")]
    [SerializeField] private GameplayStarter starter;

    /// <summary>Beers handed over, counting every hand-off even to the same enemy.</summary>
    public int EnemiesBeered { get; private set; }
    public int RefillsUsed { get; private set; }

    /// <summary>Game time since gameplay started, so pauses and freezes don't count.</summary>
    public float SecondsSurvived => hasBegun ? (hasEnded ? endTime : Time.time) - startTime : 0f;

    public bool IsCounting => hasBegun && !hasEnded;

    private BeerHandler beerHandler;
    private bool hasBegun;
    private bool hasEnded;
    private float startTime;
    private float endTime;

    private void Update()
    {
        if (!hasBegun && (!starter || starter.HasStarted)) Begin();
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
