using UnityEngine;

/// <summary>Shared mouse-look multiplier, with the original speed at the slider midpoint.</summary>
public static class MouseSensitivitySettings
{
    public const string PreferenceKey = "Controls.MouseSensitivity";
    private static float? normalizedValue;

    public static float NormalizedValue
    {
        get
        {
            if (!normalizedValue.HasValue)
                normalizedValue = Mathf.Clamp01(PlayerPrefs.GetFloat(PreferenceKey, 0.5f));
            return normalizedValue.Value;
        }
    }

    public static float Multiplier => Mathf.Pow(4f, NormalizedValue * 2f - 1f);

    public static void SetNormalizedValue(float value)
    {
        normalizedValue = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PreferenceKey, normalizedValue.Value);
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => normalizedValue = null;
}
