using UnityEngine;
using SpaceStuff;

[RequireComponent(typeof(ScaledRigidbody))]
public class RadarTarget : MonoBehaviour
{
    private uint id;

    [HideInInspector] public ScaledRigidbody scaledRigidbody { get; private set; }
    [Header("RadarTarget")] public string team;
    public AlertSystem alertSystem;
    public bool alertWhenTargeting = true;

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
    private void Start()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();

        if (!useScaleForBounds && (boundsRenderers == null || boundsRenderers.Length == 0))
        {
            boundsRenderers = scaledRigidbody.scaledTransform.GetTrackedRenderers();
        }

        inverseFixedDeltaTime = 1f / Time.fixedDeltaTime;

        id = RadarRegistry.Register(this);
    }

    private void FixedUpdate()
    {
        acceleration = (scaledRigidbody.velocity - lastVelocity) * inverseFixedDeltaTime;
        lastVelocity = scaledRigidbody.velocity;
    }

    private void OnDestroy()
    {
        RadarRegistry.Unregister(id);
    }

    public uint GetID()
    {
        return id;
    }
}
