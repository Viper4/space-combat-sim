using UnityEngine;
using SpaceStuff;
using Unity.Netcode;

[RequireComponent(typeof(DoubleRigidbody))]
public class RadarTarget : NetworkBehaviour
{
    private uint id;

    [HideInInspector] public DoubleRigidbody doubleRigidbody { get; private set; }
    [Header("RadarTarget")] public string team;
    public AlertSystem alertSystem;

    // Assuming only one player with one rader and HUD
    [HideInInspector] public RadarIcon radarIcon;
    [HideInInspector] public int turretsTargeting;
    public Vector3d acceleration {get; private set;}
    [Tooltip("RadarTarget is invisible to radar beyond this distance")] public float stealthDistance = -1f;

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

        id = RadarRegistry.Register(this);
    }

    protected virtual void FixedUpdate()
    {
        if (IsOwner || GameManager.Instance.offlineMode)
        {
            acceleration = (doubleRigidbody.velocity - lastVelocity) * inverseFixedDeltaTime;
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
}
