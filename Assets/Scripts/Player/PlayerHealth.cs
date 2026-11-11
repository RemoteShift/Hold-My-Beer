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

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    /// <summary>When false, dying leaves the player dead for whatever handles <see cref="OnDied"/>.</summary>
    public bool RespawnOnDeath { get; set; } = true;

    /// <summary>Raised with the current health, then the maximum.</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>Raised when health reaches 0.</summary>
    public event Action OnDied;

    private LocomotionController locomotion;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        locomotion = GetComponent<LocomotionController>();
        currentHealth = maxHealth;
    }

    private void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    public void TakeDamage(float damage)
    {
        if (currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"Player took {damage} damage: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Stun() { }

    public void Die()
    {
        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDied?.Invoke();

        if (!RespawnOnDeath)
        {
            Debug.Log("Player died");
            return;
        }

        Debug.Log("Player died: respawning at the start");

        locomotion.ResetPositionRotation(spawnPosition, spawnRotation);

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
