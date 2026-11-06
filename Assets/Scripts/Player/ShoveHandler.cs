using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>RMB / F / gamepad west: shove patrons or loose furniture along the camera aim.</summary>
public class ShoveHandler : MonoBehaviour
{
    public float range = 3f;
    public float cooldown = 0.45f;
    public float furnitureImpulse = 12f;
    public float patronDistance = 2.5f;
    private float nextShove;
    private void Update()
    {
        if (Mouse.current?.rightButton.wasPressedThisFrame != true &&
            Keyboard.current?.fKey.wasPressedThisFrame != true &&
            Gamepad.current?.buttonWest.wasPressedThisFrame != true) return;
        if (Time.time < nextShove || !Camera.main) return;
        var aim = Camera.main.transform;
        if (!Physics.SphereCast(aim.position, 0.25f, aim.forward, out var hit, range, ~(1 << 3), QueryTriggerInteraction.Ignore)) return;
        var enemy = hit.collider.GetComponentInParent<EnemyBrain>();
        var furniture = hit.collider.GetComponentInParent<ThrowableFurniture>();
        if (enemy)
        {
            enemy.KnockbackFrom(transform.position, patronDistance);
            nextShove = Time.time + cooldown;
        }
        else if (furniture)
        {
            var chandelier = furniture.GetComponent<FallingChandelier>();
            if (chandelier) chandelier.Drop();
            furniture.Shove((aim.forward + Vector3.up * 0.15f).normalized * furnitureImpulse);
            nextShove = Time.time + cooldown;
        }
    }
}
