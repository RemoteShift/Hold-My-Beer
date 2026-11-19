using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class PowderKegFuseVfx : MonoBehaviour
{
    [SerializeField] private PowderKegEnemy enemy;
    private CombatPixelEffect flame;

    private void LateUpdate()
    {
        bool burning = enemy && enemy.isActiveAndEnabled && !enemy.IsDead && enemy.IsLit;
        if (!burning) { if (flame) flame.gameObject.SetActive(false); return; }
        if (!flame)
        {
            var library = CombatVfx.Library;
            if (!library || !library.fuseFlame) return;
            flame = Instantiate(library.fuseFlame, transform);
            flame.name = "VFX Burning Fuse";
        }
        flame.Follow(enemy.fuseLight ? enemy.fuseLight.transform : enemy.fuseVisual ? enemy.fuseVisual : enemy.transform);
        flame.Paused = enemy.IsBeered;
        if (!flame.gameObject.activeSelf) flame.gameObject.SetActive(true);
    }
    private void OnDisable() { if (flame) flame.gameObject.SetActive(false); }
}
