using System;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class AlertSystem : NetworkBehaviour
{
    [SerializeField] private AudioSource alertAudioSource;
    [SerializeField] private AudioClip radarLockClip;
    [SerializeField] private AudioClip torpedoLockClip;
    [SerializeField] private AudioClip contactClip;
    [SerializeField] private AudioClip specialContactClip;

    private int radarLocks = 0;
    private int torpedoLocks = 0;

    private bool IsServerOrOffline => IsServerInitialized || IsOffline;

    [TargetRpc]
    private void NewContactTargetRpc(NetworkConnection conn)
    {
        alertAudioSource.PlayOneShot(contactClip);
    }

    public void NewContact()
    {
        if (!IsServerOrOffline)
            return;
        
        if (IsOwner || IsOffline)
        {
            alertAudioSource.PlayOneShot(contactClip);
        }
        else
        {
            NewContactTargetRpc(Owner);
        }
    }

    [TargetRpc]
    private void NewSpecialContactTargetRpc(NetworkConnection conn)
    {
        alertAudioSource.PlayOneShot(specialContactClip);
    }

    public void NewSpecialContact()
    {
        if (!IsServerOrOffline)
            return;
        
        if (IsOwner || IsOffline)
        {
            alertAudioSource.PlayOneShot(specialContactClip);
        }
        else
        {
            NewSpecialContactTargetRpc(Owner);
        }
    }

    private void UpdateRadarLock()
    {
        if (!IsOwner && !IsOffline)
            return;
        if (radarLocks > 0)
        {
            if (alertAudioSource.clip != radarLockClip)
                alertAudioSource.clip = radarLockClip;
            if (!alertAudioSource.isPlaying)
                alertAudioSource.Play();
        }
        else if (radarLocks <= 0)
        {
            radarLocks = 0;
            if (torpedoLocks <= 0)
                alertAudioSource.Stop();
        }
    }

    [TargetRpc]
    private void SetRadarLockTargetRpc(NetworkConnection conn, int radarLocks)
    {
        this.radarLocks = radarLocks;
        UpdateRadarLock();
    }

    public void IncrementRadarLock(int amount)
    {
        if (!IsServerOrOffline)
            return;
        radarLocks += amount;
        if (IsOwner || IsOffline)
        {
            UpdateRadarLock();
        }
        else
        {
            SetRadarLockTargetRpc(Owner, radarLocks);
        }
    }

    private void UpdateTorpedoLock()
    {
        if (!IsOwner && !IsOffline)
            return;
        if (torpedoLocks > 0)
        {
            if (alertAudioSource.clip != torpedoLockClip)
                alertAudioSource.clip = torpedoLockClip;
            if (!alertAudioSource.isPlaying)
                alertAudioSource.Play();
        }
        else if (torpedoLocks <= 0)
        {
            torpedoLocks = 0;
            if (radarLocks <= 0)
                alertAudioSource.Stop();
        }
    }

    [TargetRpc]
    private void SetTorpedoLockTargetRpc(NetworkConnection conn, int torpedoLocks)
    {
        this.torpedoLocks = torpedoLocks;
        UpdateTorpedoLock();
    }

    public void IncrementTorpedoLock(int amount)
    {
        if (!IsServerOrOffline)
            return;
        torpedoLocks += amount;
        if (IsOwner || IsOffline)
        {
            UpdateTorpedoLock();
        }
        else
        {
            SetTorpedoLockTargetRpc(Owner, torpedoLocks);
        }
    }
}
