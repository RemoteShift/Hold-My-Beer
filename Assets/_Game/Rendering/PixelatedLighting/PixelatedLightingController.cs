using UnityEngine;

/// <summary>Snaps realtime lighting samples, never geometry, texture UVs or camera pixels.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PixelatedLightingController : MonoBehaviour
{
    [Tooltip("Turn this off to compare with the original continuous URP lighting.")]
    public bool pixelatedLighting = true;

    [Min(.005f), Tooltip("Lighting-grid size in world units. Larger values make larger lighting blocks.")]
    public float cellSize = .20f;

    [Tooltip("Use a separate grid and crisp boundary for realtime cast shadows.")]
    public bool pixelatedShadowEdges = true;

    [Min(.005f), Tooltip("World-space block size for shadow outlines, independent of light falloff.")]
    public float shadowCellSize = .30f;

    [Range(2, 12), Tooltip("Brightness steps per doubling of light. Lower values make stronger bands.")]
    public int bandsPerStop = 3;

    [Range(0f, 1f), Tooltip("Strength of stepped diffuse shading; highlights and reflections stay continuous.")]
    public float bandStrength = .8f;

    private static readonly int Enabled = Shader.PropertyToID("_HMBPixelLightingEnabled");
    private static readonly int CellSize = Shader.PropertyToID("_HMBPixelLightingCellSize");
    private static readonly int Bands = Shader.PropertyToID("_HMBPixelLightingBands");
    private static readonly int Strength = Shader.PropertyToID("_HMBPixelLightingBandStrength");
    private static readonly int ShadowSize = Shader.PropertyToID("_HMBPixelShadowSize");
    private static readonly int ShadowEdges = Shader.PropertyToID("_HMBPixelShadowEdges");

    private void OnEnable() => Apply();
    private void OnValidate() => Apply();

    private void Apply()
    {
        Shader.SetGlobalFloat(Enabled, isActiveAndEnabled && pixelatedLighting ? 1f : 0f);
        Shader.SetGlobalFloat(CellSize, Mathf.Max(.005f, cellSize));
        Shader.SetGlobalFloat(Bands, Mathf.Clamp(bandsPerStop, 2, 12));
        Shader.SetGlobalFloat(Strength, Mathf.Clamp01(bandStrength));
        Shader.SetGlobalFloat(ShadowSize, Mathf.Max(.005f, shadowCellSize));
        Shader.SetGlobalFloat(ShadowEdges, pixelatedShadowEdges ? 1f : 0f);
    }

    private void OnDisable() => Shader.SetGlobalFloat(Enabled, 0f);
}
