using UnityEngine;
using SpaceStuff;
using FishNet.Object;
using FishNet;

[RequireComponent(typeof(ScaledRigidbody), typeof(ScaledTransform))]
public class FloatingWorldOrigin : NetworkBehaviour
{
    public static FloatingWorldOrigin Instance { get; private set; }

    public ScaledRigidbody scaledRigidbody {get; private set; }
    public ScaledTransform scaledTransform {get; private set; }

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        scaledTransform = GetComponent<ScaledTransform>();

        if (InstanceFinder.IsOffline)
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.Log($"[FloatingWorldOrigin] Instance already exists in Offline mode, destroying the component on {name}.");
                Destroy(this);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner)
        {
            Debug.Log($"[FloatingWorldOrigin] Set Instance to {name}.");
            Instance = this;
            ScaledSpaceVisuals.Instance.UpdateScaleFactors();
        }
        else
        {
            Destroy(this);
        }
    }

    public Vector3d GetRealCameraPosition()
    {
        return scaledTransform.realPosition + Camera.main.transform.position.ToVector3d();        
    }
}
