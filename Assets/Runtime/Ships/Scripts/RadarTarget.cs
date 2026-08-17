using UnityEngine;
using SpaceStuff;
using FishNet.Object;

[RequireComponent(typeof(ScaledRigidbody))]
public class RadarTarget : NetworkBehaviour
{
    private uint id;

    [HideInInspector] public ScaledRigidbody scaledRigidbody { get; private set; }
    [Header("RadarTarget")] public string team;
    public Radar attachedRadar;
    public bool alertWhenTargeting = true;
    [Range(0f, 1f), Tooltip("Percentage of this target's effective radar radius that is reduced by.")]
    public float stealthReduction = 0f;
    [SerializeField, Tooltip("Target pings radars for passive listening with this trigger.")] private ScaledCollider emissionTrigger;

    // Assuming only one player with one rader and HUD
    [HideInInspector] public int radarIndex = -1;
    [HideInInspector] public bool passivelyDetected;
    [HideInInspector] public bool activelyDetected;
    [HideInInspector] public int collidersInActiveRadar = 0;
    [HideInInspector] public RadarIcon radarIcon;
    [HideInInspector] public int turretsTargeting;

    public bool useScaleForBounds;
    /// <summary>
    /// Renderers to use when calculating this radar target's bounds. Set in editor. If empty, will default to all renderers tracked by the ScaledTransform.
    /// </summary>
    public Renderer[] boundsRenderers;

    private bool IsOwnerOrOffline => NetworkObject == null || IsOffline || IsOwner;
    private bool IsServerOrOffline => NetworkObject == null || IsOffline || IsServerInitialized;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        scaledRigidbody.OnScaledTriggerEnter += OnScaledTriggerEnter;
        scaledRigidbody.OnScaledTriggerExit += OnScaledTriggerExit;

        if (!useScaleForBounds && (boundsRenderers == null || boundsRenderers.Length == 0))
        {
            boundsRenderers = scaledRigidbody.scaledTransform.CloneTrackedRenderers();
        }

        id = RadarRegistry.Register(this);
    }

    private void OnDestroy()
    {
        if (scaledRigidbody != null)
        {
            scaledRigidbody.OnScaledTriggerEnter -= OnScaledTriggerEnter;
            scaledRigidbody.OnScaledTriggerExit -= OnScaledTriggerExit;
        }
        RadarRegistry.Unregister(id);
    }

    private void OnScaledTriggerEnter(ScaledCollider source, ScaledCollider other)
    {
        if (!IsServerOrOffline || emissionTrigger == null || source.id != emissionTrigger.id)
            return;
        if (other.scaledRigidbody.TryGetComponent<Radar>(out var otherRadar))
        {
            if (IsOffline || otherRadar.IsOwner)
            {
                otherRadar.AddPassiveTarget(this);
            }
            else
            {
                otherRadar.StartPing(NetworkObject.ObjectId);
            }
        }
    }

    private void OnScaledTriggerExit(ScaledCollider source, ScaledCollider other)
    {
        if (!IsServerOrOffline || emissionTrigger == null || source.id != emissionTrigger.id)
            return;
        if (other.scaledRigidbody.TryGetComponent<Radar>(out var otherRadar))
        {
            if (IsOffline || otherRadar.IsOwner)
            {
                otherRadar.RemovePassiveTarget(this);
            }
            else
            {
                otherRadar.StopPing(NetworkObject.ObjectId);
            }
        }
    }

    public uint GetID()
    {
        return id;
    }

    /// <summary>
    /// Radius that radar uses for detection after considering this target's stealth reduction and number of colliders in radar range.
    /// </summary>
    /// <returns></returns>
    public double GetEffectiveRadarRadius()
    {
        float percentVisible = (1f - stealthReduction) * (float)collidersInActiveRadar / scaledRigidbody.scaledColliders.Count;
        return scaledRigidbody.scaledTransform.realRadius * percentVisible;
    }

    public void SetEmissionActive(bool value)
    {
        if (emissionTrigger.enabled != value)
            emissionTrigger.enabled = value;
    }

    public void SetEmissionTriggerRadius(double newRadius)
    {
        if (emissionTrigger != null)
            emissionTrigger.SetRadius(newRadius);
    }

    public double GetEmissionTriggerRadius()
    {
        if (!emissionTrigger.enabled)
            return -1.0;
        return emissionTrigger.GetRadius();
    }
}
