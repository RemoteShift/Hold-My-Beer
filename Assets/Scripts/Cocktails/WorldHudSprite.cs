using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Camera-facing artwork that stays readable instead of turning edge-on in 3D.</summary>
public sealed class WorldHudSprite : MonoBehaviour
{
    private float spinSpeed, spinAngle;

    public static Transform Create(string name, Transform parent, WorldHudArt.Art art, float spin = 0f)
    {
        if (art == null || !art.sprite || !WorldHudArt.Instance) return null;
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var billboard = root.AddComponent<WorldHudSprite>();
        billboard.spinSpeed = spin;
        var graphic = new GameObject("HUD sprite");
        graphic.transform.SetParent(root.transform, false);
        graphic.transform.localScale = Vector3.one * (art.worldUnitsPerPixel * art.sprite.pixelsPerUnit);
        graphic.transform.localPosition = art.opticalOffsetPixels * art.worldUnitsPerPixel;
        var renderer = graphic.AddComponent<SpriteRenderer>();
        renderer.sprite = art.sprite;
        renderer.sharedMaterial = art.material ? art.material : WorldHudArt.Instance.spriteMaterial;
        renderer.color = Color.white;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        billboard.FaceCamera();
        return root.transform;
    }

    private void LateUpdate()
    {
        spinAngle = (spinAngle + spinSpeed * Time.deltaTime) % 360f;
        FaceCamera();
    }

    private void FaceCamera()
    {
        var camera = Camera.main;
        if (camera)
            transform.rotation = camera.transform.rotation *
                Quaternion.AngleAxis(Mathf.Round(spinAngle / 15f) * 15f, Vector3.forward);
    }
}
