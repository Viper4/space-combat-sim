using UnityEngine;

[RequireComponent(typeof(ScaledTransform))]
[RequireComponent(typeof(AudioSource))]
public class ScaledAudio : MonoBehaviour
{
    private ScaledTransform _scaledTransform;
    private AudioSource _audioSource;
    private float _originalVolume;

    private void Start()
    {
        _scaledTransform = GetComponent<ScaledTransform>();
        _audioSource = GetComponent<AudioSource>();
        _originalVolume = _audioSource.volume;
        ScaleVolume(_scaledTransform.GetScaleFactor());
        _scaledTransform.OnChangeScaleFactor += ScaleVolume;
    }

    private void OnDestroy()
    {
        _scaledTransform.OnChangeScaleFactor -= ScaleVolume;
    }

    private void ScaleVolume(double scaleFactor)
    {
        _audioSource.volume = _scaledTransform.inScaledSpace ? (float)(_originalVolume / scaleFactor) : _originalVolume;
    }
}
