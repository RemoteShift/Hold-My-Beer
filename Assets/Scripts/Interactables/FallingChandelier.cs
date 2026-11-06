using UnityEngine;

/// <summary>A shove or damage breaks the suspension; furniture impact handles the falling hit.</summary>
[RequireComponent(typeof(ThrowableFurniture))]
public class FallingChandelier : MonoBehaviour, IDamageable
{
    public void TakeDamage(float damage) { if (damage > 0f) Drop(); }
    public void Stun() { Drop(); }
    public void Die() { Drop(); }
    public void Drop()
    {
        var furniture = GetComponent<ThrowableFurniture>();
        furniture.Body.isKinematic = false;
        furniture.Body.WakeUp();
        furniture.Arm(furniture.impactDamage);
    }
}
