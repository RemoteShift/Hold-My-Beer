#ifndef HMB_PIXEL_LIGHTING_DIRECT_INCLUDED
#define HMB_PIXEL_LIGHTING_DIRECT_INCLUDED

half HMBBlockShadow(half attenuation, half effectiveStrength)
{
    if (_HMBPixelLightingEnabled < 0.5 || _HMBPixelShadowEdges < 0.5 || effectiveStrength < 0.001)
        return attenuation;
    // Remove the filtering across the silhouette, without turning a deliberately
    // partial-strength shadow into a fully black one. Shadow fading is retained.
    half floorValue = 1.0h - effectiveStrength;
    half visibility = saturate((attenuation - floorValue) / effectiveStrength);
    return lerp(floorValue, 1.0h, step(0.5h, visibility));
}

Light HMBAdditionalSampledLight(uint index, InputData lightingInput, float3 shadowPosition,
    float3 originalPosition, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
#if USE_CLUSTER_LIGHT_LOOP
    int lightIndex = index;
#else
    int lightIndex = GetPerObjectLightIndex(index);
#endif
    Light light = GetAdditionalPerObjectLight(lightIndex, lightingInput.positionWS);
    Light shadowLight = GetAdditionalPerObjectLight(lightIndex, shadowPosition);
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    half4 occlusionChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
#else
    half4 occlusionChannels = _AdditionalLightsOcclusionProbes[lightIndex];
#endif
    half attenuation = AdditionalLightShadow(lightIndex, shadowPosition, shadowLight.direction,
        shadowMask, occlusionChannels);
    half strength = GetAdditionalLightShadowParams(lightIndex).x
        * (1.0h - GetAdditionalLightShadowFade(originalPosition));
    light.shadowAttenuation = HMBBlockShadow(attenuation, strength);
#if defined(_LIGHT_COOKIES)
    light.color *= SampleAdditionalLightCookie(lightIndex, lightingInput.positionWS);
#endif
#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_AMBIENT_OCCLUSION))
        light.color *= aoFactor.directAmbientOcclusion;
#endif
    return light;
}

float3 HMBSteppedDiffuse(float3 radiance)
{
    float luminance = dot(radiance, float3(0.2126, 0.7152, 0.0722));
    float bands = max(_HMBPixelLightingBands, 2.0);
    // Logarithmic steps retain dim light instead of cutting it off at a threshold.
    // Round down: stylization must not create brighter-than-source highlights.
    float stepped = exp2(floor(log2(max(luminance, 1e-6)) * bands) / bands);
    float scale = min(stepped / max(luminance, 1e-6), 1.0);
    return radiance * lerp(1.0, scale, saturate(_HMBPixelLightingBandStrength));
}

half3 HMBLightingPhysicallyBased(BRDFData brdfData, BRDFData clearCoat,
    Light originalLight, Light sampledLight, half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularOff)
{
    // Specular remains exactly where URP evaluates it: at the real surface point.
    half3 original = LightingPhysicallyBased(brdfData, clearCoat, originalLight,
        normalWS, viewDirectionWS, clearCoatMask, specularOff);
    if (_HMBPixelLightingEnabled < 0.5)
        return original;

    float3 unshadowedOriginal = originalLight.color * originalLight.distanceAttenuation
        * saturate(dot(normalWS, originalLight.direction));
    float3 originalDiffuse = unshadowedOriginal * originalLight.shadowAttenuation;
    float3 sampledDiffuse = sampledLight.color * sampledLight.distanceAttenuation
        * saturate(dot(normalWS, sampledLight.direction)) * sampledLight.shadowAttenuation;

    // Snapping closer to a point light must not amplify inverse-square falloff.
    float cap = dot(unshadowedOriginal, float3(0.2126, 0.7152, 0.0722));
    float energy = dot(sampledDiffuse, float3(0.2126, 0.7152, 0.0722));
    sampledDiffuse *= min(1.0, cap / max(energy, 1e-6));

    // Near a bulb, a cell is large relative to light distance: blend back to
    // continuous lighting rather than allowing moving grid highlights to pop.
    float attenuationRatio = max(originalLight.distanceAttenuation, sampledLight.distanceAttenuation)
        / max(min(originalLight.distanceAttenuation, sampledLight.distanceAttenuation), 1e-6);
    float stableWeight = 1.0 - smoothstep(1.25, 2.0, attenuationRatio);
    float3 pixelDiffuse = lerp(originalDiffuse, HMBSteppedDiffuse(sampledDiffuse), stableWeight);

    half3 diffuseWeight = brdfData.diffuse;
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1.0 - NoV);
    diffuseWeight *= 1.0 - clearCoatMask * fresnel;
#endif
    return max(original + diffuseWeight * (pixelDiffuse - originalDiffuse), 0.0);
}

#endif
