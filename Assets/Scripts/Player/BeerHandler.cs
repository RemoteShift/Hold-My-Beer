using System;
using UnityEngine;

/// <summary>
/// Hands a beer to the enemy the player is looking at. This is a parry: it only works
/// while that enemy is attacking, and a press at any other time keeps the glass.
/// Unbeering a beered enemy releases them, taking back whatever is left in the glass.
/// Interacting with a tap or barrel restocks the player.
/// </summary>
[RequireComponent(typeof(BeerInventory), typeof(PlayerAim))]
public class BeerHandler : MonoBehaviour
{
    /// <summary>What the player can do with whatever they're looking at. Empty if nothing.</summary>
    public string Prompt { get; private set; } = string.Empty;
    public InputHandler.PromptAction PromptAction { get; private set; }

    /// <summary>Raised only after a beer successfully interrupts an attacking enemy.</summary>
    public event Action<EnemyBrain> OnParried;

    /// <summary>Raised when a beer is handed to an attacking enemy.</summary>
    public event Action OnBeerHanded;

    /// <summary>Raised when the player restocks at a tap or barrel.</summary>
    public event Action OnRefilled;

    [Header("Audio")]
    [SerializeField] private AudioClip refillSound;
    [SerializeField] private AudioSource sfxSource;

    private BeerInventory inventory;
    private PlayerAim aim;
    private InputHandler input;

    private void Awake()
    {
        // Legacy scene copies gain the same kit as the persistent player prefab.
        if (!GetComponent<CocktailBrewer>()) gameObject.AddComponent<CocktailBrewer>();
        inventory = GetComponent<BeerInventory>();
        aim = GetComponent<PlayerAim>();
        if (!sfxSource) sfxSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        input = InputHandler.Instance;
        input.OnBeerPressed += HandleBeerPressed;
        input.OnInteractPressed += HandleInteractPressed;
        input.OnUnbeerPressed += HandleUnbeerPressed;
    }

    private void OnDisable()
    {
        if (!input) return;

        input.OnBeerPressed -= HandleBeerPressed;
        input.OnInteractPressed -= HandleInteractPressed;
        input.OnUnbeerPressed -= HandleUnbeerPressed;
    }

    private void Update()
    {
        PromptAction = InputHandler.PromptAction.None;
        Prompt = BuildPrompt(aim.FindAimedCollider());
    }

    private void HandleBeerPressed()
    {
        if (PlayerParryFeedback.IsPlayerFrozen) return;
        var enemy = aim.FindAimed<EnemyBrain>();
        if (!enemy || !enemy.CanBeBeered || !inventory.HasBeer) return;
        if (!inventory.TryTakeRightmost(out var fill)) return;
        if (!enemy.TryReceiveBeer(fill))
        {
            inventory.TryAddGlass(fill);
            return;
        }


        OnBeerHanded?.Invoke();
        OnParried?.Invoke(enemy);
    }

    private void HandleInteractPressed()
    {
        if (PlayerParryFeedback.IsPlayerFrozen) return;
        var source = aim.FindAimed<BeerSource>();
        if (source)
        {
            if (source.TryRefill(inventory))
            {
                if (refillSound && sfxSource) sfxSource.PlayOneShot(refillSound);

                OnRefilled?.Invoke();
            }
        }
    }

    private void HandleUnbeerPressed()
    {
        if (PlayerParryFeedback.IsPlayerFrozen) return;
        var enemy = aim.FindAimed<EnemyBrain>();
        if (!enemy || !enemy.TryTakeBackBeer(out var fill)) return;

        // With no free slot the glass is dropped, but the attack still resumes
        inventory.TryAddGlass(fill);
    }

    private string BuildPrompt(Collider aimed)
    {
        if (!aimed) return string.Empty;

        var source = aimed.GetComponentInParent<BeerSource>();
        if (source) return BuildSourcePrompt(source);

        var enemy = aimed.GetComponentInParent<EnemyBrain>();
        if (!enemy) return string.Empty;

        if (enemy.IsBeered)
        {
            PromptAction = InputHandler.PromptAction.Unbeer;
            return "Take back beer";
        }

        if (!enemy.CanBeBeered) return string.Empty;
        if (!inventory.HasBeer) return string.Empty;

        PromptAction = InputHandler.PromptAction.Beer;
        return "Hand beer";
    }

    private string BuildSourcePrompt(BeerSource source)
    {
        if (source.IsEmpty) return string.Empty;
        if (inventory.IsFullyStocked) return string.Empty;

        PromptAction = InputHandler.PromptAction.Interact;
        return source.IsUnlimited ? "Refill" : $"Refill ({source.RefillsLeft} left)";
    }
}
