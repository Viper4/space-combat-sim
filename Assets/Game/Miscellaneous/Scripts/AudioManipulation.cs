using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManipulation : MonoBehaviour
{
    [SerializeField] float[] pitchRange = new float[] { 1, 1 };
    [SerializeField] float[] volumeRange = new float[] { 1, 1 };
    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.pitch = Random.Range(pitchRange[0], pitchRange[1]);
        audioSource.volume = Random.Range(volumeRange[0], volumeRange[1]);
    }
}
