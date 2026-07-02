using UnityEngine;

public static class AudioMixerVolumeUtility
{
    private const float MutedDecibels = -80f;

    /// <summary>
    /// Converts a normalized linear volume value into AudioMixer decibels.
    /// </summary>
    public static float LinearToDecibels(float linearVolume)
    {
        if (linearVolume <= 0.0001f)
        {
            return MutedDecibels;
        }

        return Mathf.Log10(Mathf.Clamp01(linearVolume)) * 20f;
    }
}
