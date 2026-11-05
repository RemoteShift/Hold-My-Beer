using System;
using UnityEngine;

/// <summary>
/// Hands a beer to the enemy the player is looking at. This is a parry: it only works
/// while that enemy is attacking, and a press at any other time keeps the glass.
/// Interacting with a beered enemy unbeers them, taking back whatever is left in the glass.
/// Interacting with a tap or barrel restocks the player.
/// </summary>
[RequireComponent(typeof(BeerInventory))]
public class BeerHandler : MonoBehaviour
{
    [SerializeField] private float handRange = 3f;
    [SerializeField] private float aimRadius = 0.3f;
    [Tooltip("Layers that can be aimed at. Leave the player's own layer out.")]
    [SerializeField] private LayerMask aimMask = ~(1 << 3);

    /// <summary>What the player can do with whatever they're looking at. Empty if nothing.</summary>
    public string Prompt { get; private set; } = string.Empty;

    /// <summary>Raised with a short message saying how a press turned out.</summary>
    public event Action<string> OnFeedback;

    private BeerInventory inventory;
    private InputHandler input;

    private void Awake()
    {
        inventory = GetComponent<BeerInventory>();
    }

    private void OnEnable()
    {
        input = InputHandler.Instance;
        input.OnBeerPressed += HandleBeerPressed;
        input.OnInteractPressed += HandleInteractPressed;
    }

    private void OnDisable()
    {
        if (!input) return;

        input.OnBeerPressed -= HandleBeerPressed;
        input.OnInteractPressed -= HandleInteractPressed;
    }

    private void Update()
    {
        Prompt = BuildPrompt(FindAimedCollider());
    }

    private void HandleBeerPressed()
    {
        var enemy = FindAimed<EnemyBrain>();
        if (!enemy) return;

        if (enemy.IsBeered)
        {
            OnFeedback?.Invoke("Already drinking");
        }
        else if (!enemy.CanBeBeered)
        {
            OnFeedback?.Invoke("Not attacking");
        }
        else if (!inventory.HasBeer)
        {
            OnFeedback?.Invoke("No beer left");
        }
        else
        {
            inventory.TryTakeFullest(out var fill);
            enemy.TryReceiveBeer(fill);
            OnFeedback?.Invoke("Parried!");
        }
    }

    private void HandleInteractPressed()
    {
        var source = FindAimed<BeerSource>();
        if (source)
        {
            if (source.TryRefill(inventory)) OnFeedback?.Invoke("Refilled");
            return;
        }

        var enemy = FindAimed<EnemyBrain>();
        if (!enemy || !enemy.TryTakeBackBeer(out var fill)) return;

        // With no free slot the glass is dropped, but the attack still resumes
        var kept = inventory.TryAddGlass(fill);
        OnFeedback?.Invoke(kept ? "Beer back" : "No room, glass dropped");
    }

    private string BuildPrompt(Collider aimed)
    {
        if (!aimed) return string.Empty;

        var source = aimed.GetComponentInParent<BeerSource>();
        if (source) return BuildSourcePrompt(source);

        var enemy = aimed.GetComponentInParent<EnemyBrain>();
        if (!enemy) return string.Empty;

        if (enemy.IsBeered) return "[E] Take back beer";
        if (!enemy.CanBeBeered) return string.Empty;

        return inventory.HasBeer ? "[LMB] Hand beer" : "No beer left";
    }

    private string BuildSourcePrompt(BeerSource source)
    {
        if (source.IsEmpty) return "Barrel empty";
        if (inventory.IsFullyStocked) return "Beers full";

        return source.IsUnlimited ? "[E] Refill" : $"[E] Refill ({source.RefillsLeft} left)";
    }

    private T FindAimed<T>() where T : Component
    {
        var aimed = FindAimedCollider();
        return aimed ? aimed.GetComponentInParent<T>() : null;
    }

    private Collider FindAimedCollider()
    {
        var mainCamera = Camera.main;
        if (!mainCamera) return null;

        var aim = mainCamera.transform;

        return Physics.SphereCast(aim.position, aimRadius, aim.forward, out var hit, handRange, aimMask,
            QueryTriggerInteraction.Ignore)
            ? hit.collider
            : null;
    }
}
