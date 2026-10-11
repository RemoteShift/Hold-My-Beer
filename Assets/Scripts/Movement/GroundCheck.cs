using System.Collections.Generic;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [Header("Slope Settings")]
    [SerializeField] private float minNormalY = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool isGrounded;

    public bool IsGrounded => isGrounded;
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    private readonly Dictionary<int, Vector3> groundContacts = new Dictionary<int, Vector3>();

    public void SetGroundedState(bool grounded)
    {
        groundContacts.Clear();
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

        int colliderId = collision.collider.GetInstanceID();
        if (bestNormalY > minNormalY)
            groundContacts[colliderId] = bestNormal;
        else
            groundContacts.Remove(colliderId);

        UpdateGroundedState();
    }

    private void OnCollisionExit(Collision collision)
    {
        groundContacts.Remove(collision.collider.GetInstanceID());
        UpdateGroundedState();
    }

    private void UpdateGroundedState()
    {
        isGrounded = groundContacts.Count > 0;
        if (!isGrounded)
        {
            GroundNormal = Vector3.up;
            return;
        }

        Vector3 bestNormal = Vector3.up;
        foreach (Vector3 normal in groundContacts.Values)
        {
            if (normal.y > bestNormal.y)
                bestNormal = normal;
        }

        GroundNormal = bestNormal;
    }
}