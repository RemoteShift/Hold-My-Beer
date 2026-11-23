using System.Collections.Generic;
using UnityEngine;

/// <summary>Applies saved audio levels while preserving each source's authored mix.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class AudioVolumeChannel : MonoBehaviour
{
    public enum Channel { Music, Sfx }
    public Channel channel;
    private AudioSource source;
    private float authoredVolume;
    private static readonly HashSet<AudioVolumeChannel> Sources = new HashSet<AudioVolumeChannel>();
    public static float MusicVolume => PlayerPrefs.GetFloat("Audio.MusicVolume", 1f);
    public static float SfxVolume => PlayerPrefs.GetFloat("Audio.SfxVolume", 1f);

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        authoredVolume = source.volume;
    }

    private void OnEnable()
    {
        Sources.Add(this);
        Apply();
    }

    private void OnDisable() { Sources.Remove(this); }

    private void Apply()
    {
        if (source) source.volume = authoredVolume * (channel == Channel.Music ? MusicVolume : SfxVolume);
    }

    public static void SetMusicVolume(float value) { SetVolume("Audio.MusicVolume", value); }
    public static void SetSfxVolume(float value) { SetVolume("Audio.SfxVolume", value); }

    private static void SetVolume(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        foreach (var source in Sources) if (source) source.Apply();
    }
}
