using System;
using UnityEngine;

/// <summary>
/// Player health. By default dying respawns the player at the scene start with full health,
/// but a scene with a game over screen can turn that off and handle <see cref="OnDied"/> instead.
/// </summary>
[RequireComponent(typeof(LocomotionController))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public float MaxHealth => maxHealth * musicHealthMultiplier;
    private float musicHealthMultiplier = 1f;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;
    public float InvulnerableSecondsLeft => Mathf.Max(0f, invulnerableUntil - Time.time);
    public bool IsInvulnerable => InvulnerableSecondsLeft > 0f;

    /// <summary>When false, dying leaves the player dead for whatever handles <see cref="OnDied"/>.</summary>
    public bool RespawnOnDeath { get; set; } = true;

    /// <summary>When false, damage stops at 1 health instead of killing, e.g. during the tutorial.</summary>
    public bool CanDie { get; set; } = true;

    /// <summary>Raised with the current health, then the maximum.</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>Raised when health reaches 0.</summary>
    public event Action OnDied;

    private LocomotionController locomotion;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float invulnerableUntil;

    private void Awake()
    {
        locomotion = GetComponent<LocomotionController>();
        currentHealth = MaxHealth;
    }

    private void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Update() => RefreshMusicHealth();

    private void RefreshMusicHealth()
    {
        var multiplier = PianoPlayerEnemy.GetPlayerBuffs(transform).maxHealth;
        if (Mathf.Approximately(multiplier, musicHealthMultiplier)) return;
        var fraction = MaxHealth > 0f ? currentHealth / MaxHealth : 0f;
        musicHealthMultiplier = multiplier;
        currentHealth = fraction * MaxHealth;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0f || IsInvulnerable || damage <= 0f || float.IsNaN(damage)) return;

        RefreshMusicHealth();
        damage /= PianoPlayerEnemy.GetPlayerBuffs(transform).defense;
        currentHealth = Mathf.Max(CanDie ? 0f : 1f, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        Debug.Log($"Player took {damage} damage: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Stun() { }

    public void GrantInvulnerability(float seconds)
    {
        if (seconds > 0f) invulnerableUntil = Time.time + seconds;
    }

    public void ClearInvulnerability()
    {
        invulnerableUntil = 0f;
    }

    public void RestoreFullHealth()
    {
        currentHealth = MaxHealth;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    public void Die()
    {
        ClearInvulnerability();
        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        OnDied?.Invoke();

        if (!RespawnOnDeath)
        {
            Debug.Log("Player died");
            return;
        }

        Debug.Log("Player died: respawning at the start");

        locomotion.ResetPositionRotation(spawnPosition, spawnRotation);

        currentHealth = MaxHealth;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }
}
