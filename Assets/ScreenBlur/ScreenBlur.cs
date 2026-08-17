using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
[VolumeComponentMenu("Post-processing/Screen Blur")]
public class ScreenBlur : VolumeComponent
{
    public ClampedFloatParameter radius =
        new ClampedFloatParameter(
            5f,
            0f,
            20f
        );

    public ClampedFloatParameter strength =
        new ClampedFloatParameter(
            1f,
            0f,
            1f
        );

    public bool IsActive()
    {
        return strength.value > 0f && radius.value > 0f;
    }
}