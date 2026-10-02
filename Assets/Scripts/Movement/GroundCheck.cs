using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [Header("Slope Settings")]
    [SerializeField] private float minNormalY = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool isGrounded;

    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    public void SetGroundedState(bool grounded)
    {
        isGrounded = grounded;
        if (!grounded)
        {
            GroundNormal = Vector3.up;
        }
    }
    
    private void OnCollisionStay(Collision collision)
    {
        Vector3 bestNormal = Vector3.zero;
        float bestNormalY = minNormalY;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            if (contact.normal.y > bestNormalY)
            {
                bestNormal = contact.normal;
                bestNormalY = contact.normal.y;
            }
        }

        isGrounded = bestNormalY > minNormalY;

        if (isGrounded)
            GroundNormal = bestNormal;
    }

    private void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
        GroundNormal = Vector3.up;
    }
}