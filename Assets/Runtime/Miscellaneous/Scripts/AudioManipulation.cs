using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManipulation : MonoBehaviour
{
    [SerializeField] private bool setPitchOnAwake = true;
    [SerializeField] private bool setVolumeOnAwake = true;
    [SerializeField] private float[] pitchRange = new float[] { 1, 1 };
    [SerializeField] private float[] volumeRange = new float[] { 1, 1 };
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (setPitchOnAwake)
            audioSource.pitch = Random.Range(pitchRange[0], pitchRange[1]);
        if (setVolumeOnAwake)
            audioSource.volume = Random.Range(volumeRange[0], volumeRange[1]);
    }

    public void ChangePitch()
    {
        audioSource.pitch = Random.Range(pitchRange[0], pitchRange[1]);
    }

    public void ChangeVolume()
    {
        audioSource.volume = Random.Range(volumeRange[0], volumeRange[1]);
    }

    public void ResetPlay(bool change)
    {
        if (audioSource.isPlaying)
            audioSource.Stop();
        if (change)
        {
            ChangePitch();
            ChangeVolume();
        }
        audioSource.Play();
    }

    public void TryPlay(bool change)
    {
        if (audioSource.isPlaying)
            return;
        if (change)
        {
            ChangePitch();
            ChangeVolume();
        }
        audioSource.Play();
    }
}
