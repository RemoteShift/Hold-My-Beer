using System;
using UnityEngine;

/// <summary>Shoves the furniture or beered enemy the player is looking at, away from them.</summary>
[RequireComponent(typeof(PlayerAim))]
public class PushHandler : MonoBehaviour
{
    [Tooltip("Shove distance is this divided by the object's weight.")]
    [SerializeField, Min(0f)] private float pushStrength = 30f;
    [Tooltip("Seconds after a successful push before the player can push again.")]
    [SerializeField, Min(0f)] private float pushCooldown = 1f;

    /// <summary>What the player can push right now. Empty if nothing.</summary>
    public string Prompt { get; private set; } = string.Empty;

    /// <summary>Raised with a short message saying how a press turned out.</summary>
    public event Action<string> OnFeedback;

    private PlayerAim aim;
    private InputHandler input;
    private float nextPushTime;

    private void Awake()
    {
        aim = GetComponent<PlayerAim>();
    }

    private void OnEnable()
    {
        input = InputHandler.Instance;
        input.OnPushPressed += HandlePushPressed;
    }

    private void OnDisable()
    {
        if (input) input.OnPushPressed -= HandlePushPressed;
    }

    private void Update()
    {
        Prompt = Time.time < nextPushTime ? string.Empty : BuildPrompt(aim.FindAimedCollider());
    }

    private void HandlePushPressed()
    {
        if (Time.time < nextPushTime) return;

        var pushable = aim.FindAimed<Pushable>();
        if (pushable)
        {
            if (pushable.TryPush(aim.FlatForward, pushStrength)) StartCooldown();
            return;
        }

        var enemy = aim.FindAimed<EnemyBrain>();
        if (!enemy) return;

        if (enemy.TryPush(aim.FlatForward, pushStrength))
        {
            StartCooldown();
        }
        else
        {
            OnFeedback?.Invoke("Only beered enemies");
        }
    }

    private void StartCooldown()
    {
        nextPushTime = Time.time + pushCooldown;
    }

    private static string BuildPrompt(Collider aimed)
    {
        if (!aimed) return string.Empty;

        var pushable = aimed.GetComponentInParent<Pushable>();
        if (pushable) return pushable.IsSliding ? string.Empty : "[RMB] Push";

        var enemy = aimed.GetComponentInParent<EnemyBrain>();
        return enemy && enemy.IsBeered ? "[RMB] Push" : string.Empty;
    }
}
