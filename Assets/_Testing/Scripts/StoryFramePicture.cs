using UnityEngine;

/// <summary>
/// Gives this frame its own picture without touching the shared material.
/// Assign an image on each frame INSTANCE in the scene (not on the prefab asset), and the
/// "Picture" quad inside this frame shows it. Leave it empty to keep the placeholder look.
///
/// Uses a MaterialPropertyBlock, so no material copies or new material assets are created.
/// </summary>
[ExecuteAlways]
public class StoryFramePicture : MonoBehaviour
{
    private const string PictureChildName = "Picture";

    // URP Lit / Unlit use _BaseMap and _BaseColor. _MainTex and _Color cover built-in style shaders.
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Tooltip("The story image shown in this frame.")]
    [SerializeField] private Texture2D image;

    private MaterialPropertyBlock block;

    /// <summary>The image currently assigned to this frame.</summary>
    public Texture2D Image => image;

    /// <summary>Change the image from code.</summary>
    public void SetImage(Texture2D newImage)
    {
        image = newImage;
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        // Runs when you change the slot in the Inspector, so the Scene view updates immediately.
        Apply();
    }

    private void Apply()
    {
        // Skip the prefab asset itself (only scene objects and the prefab stage have a valid scene).
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        Transform picture = transform.Find(PictureChildName);
        if (picture == null)
        {
            return;
        }

        Renderer pictureRenderer = picture.GetComponent<Renderer>();
        if (pictureRenderer == null)
        {
            return;
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }

        pictureRenderer.GetPropertyBlock(block);

        if (image != null)
        {
            block.SetTexture(BaseMapId, image);
            block.SetTexture(MainTexId, image);
            // The placeholder material is tinted beige; force white so the image shows true colors.
            block.SetColor(BaseColorId, Color.white);
            block.SetColor(ColorId, Color.white);
            pictureRenderer.SetPropertyBlock(block);
        }
        else
        {
            // No image: clear the override so the placeholder material shows as before.
            pictureRenderer.SetPropertyBlock(null);
        }
    }
}
