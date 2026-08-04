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
                scaledTransform.inScaledSpace = false;
                scaledRigidbody.active = true;
                transform.position = Vector3.zero;
            }
            else
            {
                Debug.Log(GameLog.ObjectLog(this, $"Instance already exists in Offline mode, destroying the component on {name}."));
                Destroy(this);
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (IsOwner)
        {
            Instance = this;
            // Things could have set this object's realPosition to something before Instance was set, so set its position to zero and force world space
            transform.position = Vector3.zero;
            scaledTransform.inScaledSpace = false;
            scaledRigidbody.active = true;
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
