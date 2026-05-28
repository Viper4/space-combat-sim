using UnityEngine;
using SpaceStuff;
using Unity.Netcode;

[RequireComponent(typeof(DoubleRigidbody))]
public class RadarTarget : NetworkBehaviour
{
    private uint id;

    [HideInInspector] public DoubleRigidbody doubleRigidbody;
    [Header("RadarTarget")] public string team;

    // Assuming only one radar
    [HideInInspector] public RadarIcon radarIcon;    
    [HideInInspector] public Color originalHUDColor;
    [HideInInspector] public Color originalRadarColor;
    [HideInInspector] public Color originalRadarEmission;
    [HideInInspector] public uint turretsTargeting;

    private Metrics metrics;
    private Vector3d lastVelocity;
    private float inverseFixedDeltaTime;

    public bool useScaleForBounds;
    /// <summary>
    /// Renderers to use when calculating this radar target's bounds. Set in editor. If empty, will default to all renderers tracked by the ScaledTransform.
    /// </summary>
    public Renderer[] boundsRenderers;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        doubleRigidbody = GetComponent<DoubleRigidbody>();

        if (!useScaleForBounds && (boundsRenderers == null || boundsRenderers.Length == 0))
        {
            boundsRenderers = doubleRigidbody.scaledTransform.GetTrackedRenderers();
        }

        inverseFixedDeltaTime = 1f / Time.fixedDeltaTime;
        metrics = new Metrics
        {
            acceleration = Vector3d.zero,
            direction = Vector3.zero,
            distance = -1f,
            speed = 0f,
            arrivalTime = -1f
        };

        id = RadarRegistry.Register(this);
    }

    protected virtual void FixedUpdate()
    {
        if (IsOwner || GameManager.Instance.offlineMode)
        {
            metrics.acceleration = (doubleRigidbody.velocity - lastVelocity) * inverseFixedDeltaTime;
            lastVelocity = doubleRigidbody.velocity;
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        RadarRegistry.Unregister(id);
    }

    public uint GetID()
    {
        return id;
    }

    /// <summary>
    /// Returns the current metrics of this radar target. Metrics should be relative to the Owner's ship
    /// </summary>
    /// <returns></returns>
    public Metrics GetMetrics()
    {
        return metrics;
    }

    /// <summary>
    /// Updates the metrics of this radar target. 
    /// Should be called by the Owner's ship after calculating relative metrics for this target like in the Radar.
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="distance"></param>
    /// <param name="speed"></param>
    /// <param name="closingSpeed"></param>
    /// <param name="arrivalTime"></param>
    public void UpdateMetrics(Vector3 direction, float distance, float speed, float closingSpeed, float arrivalTime)
    {
        metrics.direction = direction;
        metrics.distance = distance;
        metrics.speed = speed;
        metrics.closingSpeed = closingSpeed;
        metrics.arrivalTime = arrivalTime;
    }

    public struct Metrics
    {
        public Vector3d acceleration;
        public Vector3 direction;
        public float distance;
        public float speed;
        public float closingSpeed;
        public float arrivalTime;
    }
}
