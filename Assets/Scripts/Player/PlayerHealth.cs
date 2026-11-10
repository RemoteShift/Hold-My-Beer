using System;
using UnityEngine;

/// <summary>Player health. Dying respawns the player at the scene start with full health.</summary>
[RequireComponent(typeof(LocomotionController))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    /// <summary>When false, damage stops at 1 health instead of killing, e.g. during the tutorial.</summary>
    public bool CanDie { get; set; } = true;

    /// <summary>Raised with the current health, then the maximum.</summary>
    public event Action<float, float> OnHealthChanged;

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

        currentHealth = Mathf.Max(CanDie ? 0f : 1f, currentHealth - damage);
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
        Debug.Log("Player died: respawning at the start");

        locomotion.ResetPositionRotation(spawnPosition, spawnRotation);

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
