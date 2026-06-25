using System;
using FishNet.Object;
using UnityEngine;

public class NetworkObjectDestroyer : MonoBehaviour
{
    [SerializeField] private NetworkObject networkObject;
    [SerializeField] private UnityEngine.Object[] nonOwnerToDestroy;
    [SerializeField] private Behaviour[] nonOwnerToDisable;
    [SerializeField] private UnityEngine.Object[] nonServerToDestroy;
    [SerializeField] private Behaviour[] nonServerToDisable;

    public void DestroyAndDisable()
    {
        if (networkObject.IsOffline)
            return;
        Debug.Log($"[NetworkObjectDestroyer] Destroying and Disabling objects and behaviours for {name}.");
        if (!networkObject.IsOwner)
        {
            for(int i = 0; i < nonOwnerToDestroy.Length; i++)
            {
                Destroy(nonOwnerToDestroy[i]);
            }
            for(int i = 0; i < nonOwnerToDisable.Length; i++)
            {
                nonOwnerToDisable[i].enabled = false;
            }
        }

        if (!networkObject.IsServerInitialized)
        {
            for(int i = 0; i < nonServerToDestroy.Length; i++)
            {
                Destroy(nonServerToDestroy[i]);
            }
            for(int i = 0; i < nonServerToDisable.Length; i++)
            {
                nonServerToDisable[i].enabled = false;
            }
        }
    }
}
