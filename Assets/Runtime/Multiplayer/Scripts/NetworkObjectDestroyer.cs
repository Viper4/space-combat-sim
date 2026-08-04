using System;
using FishNet;
using FishNet.Object;
using UnityEngine;

public class NetworkObjectDestroyer : NetworkBehaviour
{
    [Header("Non-Owner")]
    [SerializeField] private GameObject[] nonOwnerGOsToDestroy;
    [SerializeField] private Behaviour[] nonOwnerBehaviorsToDestroy;

    [Header("Non-Server")]
    [SerializeField] private GameObject[] nonServerGOsToDestroy;
    [SerializeField] private Behaviour[] nonServerBehaviorsToDestroy;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        HideCriticalObjects();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Initialize();
    }

    public void HideCriticalObjects()
    {
        Debug.Log(GameLog.ObjectLog(this, $"Hid critical objects on {name}"));
        // Keep everything disabled for safety until client starts
        for(int i = 0; i < nonOwnerGOsToDestroy.Length; i++)
        {
            nonOwnerGOsToDestroy[i].SetActive(false);
        }
        for(int i = 0; i < nonOwnerBehaviorsToDestroy.Length; i++)
        {
            nonOwnerBehaviorsToDestroy[i].enabled = false;
        }

        for(int i = 0; i < nonServerGOsToDestroy.Length; i++)
        {
            nonServerGOsToDestroy[i].SetActive(false);
        }
        for(int i = 0; i < nonServerBehaviorsToDestroy.Length; i++)
        {
            nonServerBehaviorsToDestroy[i].enabled = false;
        }
    }

    private void ShowOwnerObjects()
    {
        Debug.Log(GameLog.ObjectLog(this, $"Showing Owner objects on {name}."));
        for(int i = 0; i < nonOwnerGOsToDestroy.Length; i++)
        {
            nonOwnerGOsToDestroy[i].SetActive(true);
        }
        for(int i = 0; i < nonOwnerBehaviorsToDestroy.Length; i++)
        {
            nonOwnerBehaviorsToDestroy[i].enabled = true;
        }
    }

    private void ShowServerObjects()
    {
        Debug.Log(GameLog.ObjectLog(this, $"Showing Server objects on {name}."));
        for(int i = 0; i < nonServerGOsToDestroy.Length; i++)
        {
            nonServerGOsToDestroy[i].SetActive(true);
        }
        for(int i = 0; i < nonServerBehaviorsToDestroy.Length; i++)
        {
            nonServerBehaviorsToDestroy[i].enabled = true;
        }
    }

    public void Initialize()
    {
        if (IsOffline)
        {
            ShowOwnerObjects();
            ShowServerObjects();
            return;
        }

        if (!IsOwner)
        {
            Debug.Log(GameLog.ObjectLog(this, $"Destroying non-owner objects for {name}."));
            for(int i = 0; i < nonOwnerGOsToDestroy.Length; i++)
            {
                Destroy(nonOwnerGOsToDestroy[i]);
            }
            for(int i = 0; i < nonOwnerBehaviorsToDestroy.Length; i++)
            {
                Destroy(nonOwnerBehaviorsToDestroy[i]);
            }
        }
        else
        {
            ShowOwnerObjects();
        }

        if (!IsServerInitialized)
        {
            Debug.Log(GameLog.ObjectLog(this, $"Destroying non-server objects for {name}."));
            for(int i = 0; i < nonServerGOsToDestroy.Length; i++)
            {
                Destroy(nonServerGOsToDestroy[i]);
            }
            for(int i = 0; i < nonServerBehaviorsToDestroy.Length; i++)
            {
                Destroy(nonServerBehaviorsToDestroy[i]);
            }
        }
        else
        {
            ShowServerObjects();
        }
    }
}
