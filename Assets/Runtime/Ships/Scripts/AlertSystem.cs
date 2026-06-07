using System;
using UnityEngine;

public class AlertSystem : MonoBehaviour
{
    [SerializeField] private AudioSource alertAudioSource;
    [SerializeField] private AudioClip radarLockClip;
    [SerializeField] private AudioClip torpedoLockClip;
    [SerializeField] private AudioClip contactClip;
    [SerializeField] private AudioClip specialContactClip;

    private int radarLocks = 0;
    private int torpedoLocks = 0;

    public void NewContact()
    {
        alertAudioSource.PlayOneShot(contactClip);
    }

    public void NewSpecialContact()
    {
        alertAudioSource.PlayOneShot(specialContactClip);
    }

    private bool TryPlayRadarLock()
    {
        if (radarLocks > 0)
        {
            if (alertAudioSource.clip != radarLockClip)
                alertAudioSource.clip = radarLockClip;
            if (!alertAudioSource.isPlaying)
                alertAudioSource.Play();
            return true;
        }
        return false;
    }

    public void AddRadarLock()
    {
        radarLocks++;
        TryPlayRadarLock();
    }

    public void RemoveRadarLock()
    {
        radarLocks--;
        if(radarLocks <= 0)
        {
            radarLocks = 0;
            if (!TryPlayTorpedoLock())
                alertAudioSource.Stop();
        }
    }

    private bool TryPlayTorpedoLock()
    {
        if (torpedoLocks > 0)
        {
            if (alertAudioSource.clip != torpedoLockClip)
                alertAudioSource.clip = torpedoLockClip;
            if (!alertAudioSource.isPlaying)
                alertAudioSource.Play();
            return true;
        }
        return false;
    }

    public void AddTorpedoLock()
    {
        torpedoLocks++;
        TryPlayTorpedoLock();
    }
    
    public void RemoveTorpedoLock()
    {
        torpedoLocks--;
        if (torpedoLocks <= 0)
        {
            torpedoLocks = 0;
            if (!TryPlayRadarLock())
                alertAudioSource.Stop();
        }
    }
}
