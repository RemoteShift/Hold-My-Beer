#ifndef HMB_PIXEL_LIGHTING_SAMPLING_INCLUDED
#define HMB_PIXEL_LIGHTING_SAMPLING_INCLUDED

// Global rather than material properties: the scene controller is the single switch.
float _HMBPixelLightingEnabled;
float _HMBPixelLightingCellSize;
float _HMBPixelLightingBands;
float _HMBPixelLightingBandStrength;
float _HMBPixelShadowSize;
float _HMBPixelShadowEdges;

float3 HMBGridSamplePosition(float3 originalPosition, float3 surfaceNormal, float gridSize)
{
    if (_HMBPixelLightingEnabled < 0.5)
        return originalPosition;

    float cellSize = max(gridSize, 0.005);
    float3 samplePosition = floor(originalPosition / cellSize + 0.5) * cellSize;

    // Re-project onto the triangle plane. This keeps samples on the receiving
    // surface instead of snapping them behind walls or beneath the floor.
    // Interpolated geometric normals avoid view-dependent screen derivatives.
    float3 planeNormal = normalize(surfaceNormal);
    samplePosition += planeNormal * dot(originalPosition - samplePosition, planeNormal);

    return samplePosition;
}

float3 HMBLightingSamplePosition(float3 positionWS, float3 surfaceNormal)
{
    return HMBGridSamplePosition(positionWS, surfaceNormal, _HMBPixelLightingCellSize);
}

float3 HMBShadowSamplePosition(float3 positionWS, float3 surfaceNormal)
{
    if (_HMBPixelShadowEdges < 0.5) return positionWS;
    return HMBGridSamplePosition(positionWS, surfaceNormal, _HMBPixelShadowSize);
}

#endif
