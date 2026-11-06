using UnityEngine;

/// <summary>Keep furniture packaged with its patron in the prefab, but independent in play.</summary>
public class DetachFurnitureOnStart : MonoBehaviour
{
    private void Start() { transform.SetParent(null, true); }
}
