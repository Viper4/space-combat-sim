using UnityEngine;
using SpaceStuff;
using Unity.Netcode;

public class RadarTarget : NetworkBehaviour
{
    public uint id;

    [Header("RadarTarget")] public ScaledTransform scaledTransform;
    public DoubleRigidbody doubleRigidbody;
    public Rigidbody attachedRB;
    public string team;

    // Assuming only one radar
    [HideInInspector] public RadarIcon radarIcon;
    [HideInInspector] public Vector3 direction;
    [HideInInspector] public float arrivalTime;
    [HideInInspector] public float distance;
    [HideInInspector] public float speed;
    [HideInInspector] public Color originalHUDColor;
    [HideInInspector] public Color originalRadarColor;
    [HideInInspector] public Color originalRadarEmission;
    [HideInInspector] public uint turretsTargeting;

    public Vector3 velocity { get; private set; }
    private Vector3 lastVelocity;
    public Vector3 acceleration { get; private set; }
    private bool calculateAcceleration = true;
    private float inverseFixedDeltaTime;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        if(scaledTransform == null)
            TryGetComponent(out scaledTransform);
        if(doubleRigidbody == null)
            TryGetComponent(out doubleRigidbody);
        if(attachedRB == null)
            TryGetComponent(out attachedRB);

        if (attachedRB == null && doubleRigidbody == null)
            calculateAcceleration = false;

        inverseFixedDeltaTime = 1f / Time.fixedDeltaTime;

        id = RadarRegistry.Register(this);
    }

    protected virtual void FixedUpdate()
    {
        if ((IsOwner || GameManager.Instance.offlineMode) && calculateAcceleration)
        {
            if (doubleRigidbody != null)
            {
                velocity = attachedRB.linearVelocity;
            }
            else if (attachedRB != null)
            {
                velocity = doubleRigidbody.velocity.ToVector3();
            }
            acceleration = (velocity - lastVelocity) * inverseFixedDeltaTime;
            lastVelocity = velocity;
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
