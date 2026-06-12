using System;
using FishNet.Object;
using UnityEngine;

public class OwnerObjectManager : MonoBehaviour
{
    [SerializeField] private NetworkObject networkObject;
    [SerializeField] private bool destroyNonOwner;
    [SerializeField] private bool disableNonOwner;
    [SerializeField] private UnityEngine.Object[] toDestroy;
    [SerializeField] private Behaviour[] toDisable;

    public void DestroyAndDisable()
    {
        if (!networkObject.IsOwner)
        {
            if (destroyNonOwner)
            {
                for(int i = 0; i < toDestroy.Length; i++)
                {
                    Destroy(toDestroy[i]);
                }
            }
            if (disableNonOwner)
            {
                for(int i = 0; i < toDisable.Length; i++)
                {
                    toDisable[i].enabled = false;
                }
            }
        }
    }
}
