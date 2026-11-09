using System;
using UnityEngine;

/// <summary>One input/cooldown path for furniture, chandeliers, and beered patrons.</summary>
[RequireComponent(typeof(PlayerAim))]
[DisallowMultipleComponent]
public class PushHandler : MonoBehaviour
{
    [Tooltip("Enemy/slide travel = strength divided by mass.")]
    [SerializeField, Min(0f)] private float pushStrength = 30f;
    [SerializeField, Min(0f)] private float pushCooldown = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip beerPushSound;
    [SerializeField] private AudioClip furniturePushSound;

    public string Prompt { get; private set; } = string.Empty;
    public event Action<string> OnFeedback;
    private PlayerAim aim;
    private AudioSource audioSource;
    private InputHandler input;
    private float nextPushTime;

    private void Awake()
    {
        aim = GetComponent<PlayerAim>();
        audioSource = GetComponent<AudioSource>();
    }
    private void OnEnable()
    {
        input = InputHandler.Instance;
        input.OnPushPressed += HandlePushPressed;
    }
    private void OnDisable() { if (input) input.OnPushPressed -= HandlePushPressed; }
    private void Update() { Prompt = Time.time < nextPushTime ? string.Empty : BuildPrompt(aim.FindAimedCollider()); }
    private void HandlePushPressed() { TryPushAimed(); }

    public bool TryPushAimed()
    {
        if (Time.time < nextPushTime) return false;
        var hit = aim.FindAimedCollider();
        if (!hit) return false;
        var enemy = hit.GetComponentInParent<EnemyBrain>();
        var succeeded = false;
        if (enemy)
        {
            succeeded = enemy.TryPush(aim.FlatForward, pushStrength);
            if (succeeded) { if (beerPushSound && audioSource) audioSource.PlayOneShot(beerPushSound); }
            else OnFeedback?.Invoke(enemy.IsBeingPushed ? "Already sliding" : "Only beered enemies");
        }
        else
        {
            var chandelier = hit.GetComponentInParent<FallingChandelier>();
            var pushable = hit.GetComponentInParent<Pushable>();
            var furniture = hit.GetComponentInParent<ThrowableFurniture>();
            if (chandelier) { chandelier.Drop(); succeeded = true; }
            else if (pushable) succeeded = pushable.TryPush(aim.FlatForward, pushStrength);
            else if (furniture && !furniture.IsHeld && !furniture.Body.isKinematic)
            {
                furniture.Shove((aim.FlatForward + Vector3.up * 0.15f).normalized * pushStrength);
                succeeded = true;
            }
            if (succeeded && furniturePushSound && audioSource) audioSource.PlayOneShot(furniturePushSound);
        }
        if (succeeded) nextPushTime = Time.time + pushCooldown;
        return succeeded;
    }

    private static string BuildPrompt(Collider aimed)
    {
        if (!aimed) return string.Empty;
        var enemy = aimed.GetComponentInParent<EnemyBrain>();
        if (enemy) return enemy.IsBeered && !enemy.IsBeingPushed && !enemy.IsImpactStunned ? "[RMB] Push" : string.Empty;
        if (aimed.GetComponentInParent<FallingChandelier>()) return "[RMB] Drop chandelier";
        var pushable = aimed.GetComponentInParent<Pushable>();
        if (pushable) return pushable.CanPush ? "[RMB] Push" : string.Empty;
        var furniture = aimed.GetComponentInParent<ThrowableFurniture>();
        return furniture && !furniture.IsHeld && !furniture.Body.isKinematic ? "[RMB] Push" : string.Empty;
    }
}
