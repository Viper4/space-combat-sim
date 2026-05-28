using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;

public class Turret : MonoBehaviour
{
    [Header("Turret")] public StatSystem statSystem;
    protected TurretSystem turretSystem;

    public bool active = true;
    [SerializeField] protected LayerMask ignoreLayers;
    [SerializeField] private Transform origin;
    public Transform platform;
    public Transform barrel;
    public Transform firePoint;
    [SerializeField] private Transform casingPoint;
    [SerializeField] private float casingRandomness = 0.1f;

    [SerializeField] private Vector3 minAngles;
    [SerializeField] private Vector3 maxAngles;
    [SerializeField] private float rotateSpeed = 180f;

    [SerializeField] private float maxShootDelta = 0.05f;
    [SerializeField, Tooltip("One bullet per fireRate seconds.")] private float fireRate = 0.15f;
    protected float fireTime = 0;
    [SerializeField] protected GameObject projectilePrefab;
    [SerializeField] private GameObject shootParticles;
    [SerializeField] protected float projectileSpeed = 50;
    [SerializeField, Tooltip("0 for no tracer. Otherwise 1 tracer every tracerInterval shots.")] protected int tracerInterval = 3;
    protected int tracerCounter = 0;

    [SerializeField] private GameObject casingPrefab;
    [SerializeField] private float casingSpeed = 5;

    public RadarTarget currentTarget;
    private RadarTarget previousTarget = null;

    [SerializeField] private string[] targetTags;
    [SerializeField] private string explosiveTag;
    private HashSet<string> targetTagsSet = new HashSet<string>();

    private List<uint> validTargets = new List<uint>();

    public float explosionRadius = 20f;

    [HideInInspector] public Vector3 aimDirection;
    [HideInInspector] public bool fire = false;
    [SerializeField] protected bool showLines;

    public GameObject UIModel;

    public bool destroyed = false;
    [SerializeField, Tooltip("Percent of health lost before enabling damaged particles.")] private float damagedThreshold = 0.5f;
    [SerializeField] private ParticleSystem damagedParticles;
    [SerializeField] private GameObject aliveGameObject;
    [SerializeField] private GameObject destroyedGameObject;

    private List<Collider> ignoreColliders = new List<Collider>();

    protected virtual void Start()
    {
        statSystem = GetComponent<StatSystem>();
        turretSystem = GetComponentInParent<TurretSystem>();

        foreach (string tag in targetTags)
        {
            targetTagsSet.Add(tag);
        }
    }

    protected virtual void Update()
    {
        if (active)
        {
            if (fire)
            {
                fireTime += Time.deltaTime;
                if (fireTime >= fireRate)
                {
                    if ((firePoint.forward - aimDirection).sqrMagnitude < maxShootDelta * maxShootDelta)
                    {
                        Fire();
                    }
                }
            }
            if (aimDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(aimDirection, platform.up);
                platform.rotation = barrel.rotation = Quaternion.RotateTowards(barrel.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                platform.localEulerAngles = new Vector3(0, platform.localEulerAngles.y, 0);
                barrel.localEulerAngles = SpaceMath.Clamp(barrel.localEulerAngles.NormalizeEulerAngles(), minAngles, maxAngles);
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        if (active)
        {
            if (!turretSystem.manualControl)
            {
                fire = false;
                if (validTargets.Count > 0)
                {
                    SelectTarget();

                    if (currentTarget == null)
                        return;

                    RadarTarget.Metrics shipMetrics = turretSystem.ship.GetMetrics();
                    RadarTarget.Metrics targetMetrics = currentTarget.GetMetrics();

                    // Calculate future position of target
                    Vector3d originOffset = turretSystem.ship.transform.InverseTransformPoint(origin.position).ToVector3d();
                    Vector3d realOriginPos = turretSystem.ship.doubleRigidbody.scaledTransform.realPosition + originOffset;
                    Vector3d realTargetPos = currentTarget.doubleRigidbody.scaledTransform.realPosition;
                    Vector3d relativePosition = realTargetPos - realOriginPos;
                    Vector3d relativeVelocity = currentTarget.doubleRigidbody.velocity - turretSystem.ship.doubleRigidbody.velocity;
                    Vector3d relativeAcceleration = targetMetrics.acceleration - shipMetrics.acceleration;
                    double distance = relativePosition.magnitude;
                    float predictionTime = (float)(distance / projectileSpeed);
                    Vector3d predictedRelativePosition = relativePosition + (relativeVelocity * predictionTime) + (0.5 * predictionTime * predictionTime * relativeAcceleration);
                    for (int i = 1; i < 5; i++)
                    {
                        predictedRelativePosition = relativePosition + (relativeVelocity * predictionTime) + (0.5 * predictionTime * predictionTime * relativeAcceleration);
                        predictionTime = (float)(predictedRelativePosition.magnitude / projectileSpeed);
                    }

                    // futureTargetPosition = realTargetPos + (relativeVelocity * predictionTime) + (0.5 * predictionTime * predictionTime * relativeAcceleration);
                    Ray ray = new Ray(firePoint.position, predictedRelativePosition.ToVector3());
                    if (!Physics.Raycast(ray, out RaycastHit hit, 1.0f, ~ignoreLayers) || hit.transform == currentTarget.transform)
                        fire = true;

                    if (showLines)
                    {
                        Debug.DrawRay(ray.origin, ray.direction, Color.green, Time.fixedDeltaTime);
                    }
                }
            }
        }
    }

    private void SelectTarget()
    {
        RadarTarget bestTarget = previousTarget;
        float smallestArrivalTime = float.MaxValue;
        for (int i = validTargets.Count - 1; i >= 0; i--)
        {
            if (!RadarRegistry.TryGet(validTargets[i], out var radarTarget))
            {
                validTargets.RemoveAt(i);
                continue;
            }
            Vector3d originOffset = turretSystem.ship.transform.InverseTransformPoint(origin.position).ToVector3d();
            Vector3d realOriginPos = turretSystem.ship.doubleRigidbody.scaledTransform.realPosition + originOffset;

            Vector3d direction = (radarTarget.doubleRigidbody.scaledTransform.realPosition - realOriginPos).normalized;
            if (Physics.Raycast(realOriginPos.ToVector3(), direction.ToVector3(), out RaycastHit hit, 10f, ~ignoreLayers) && hit.rigidbody != radarTarget)
                continue; // Obstructed view of target

            RadarTarget.Metrics targetMetrics = radarTarget.GetMetrics();

            if (targetMetrics.closingSpeed + projectileSpeed <= 0f)
                continue; // If projectile was fired directly at target, closingSpeed still <= 0 (moving away or stationary relative to projectile)

            float sqrDst = (radarTarget.transform.position - barrel.position).sqrMagnitude;
            if (targetMetrics.arrivalTime < smallestArrivalTime && (sqrDst > explosionRadius * explosionRadius || !radarTarget.transform.CompareTag(explosiveTag)))
            {
                bestTarget = radarTarget;
                smallestArrivalTime = targetMetrics.arrivalTime;
            }
        }
        currentTarget = bestTarget;
        if (currentTarget != null)
        {
            previousTarget = currentTarget;
        }
    }

    protected virtual void Fire()
    {
        tracerCounter++;
        fireTime = 0;
        Rigidbody projectileRB = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation).GetComponent<Rigidbody>();
        Collider projectileCollider = projectileRB.GetComponent<Collider>();
        foreach(Collider collider in ignoreColliders)
        {
            Physics.IgnoreCollision(projectileCollider, collider, true);
        }
        projectileRB.linearVelocity = turretSystem.ship.doubleRigidbody.velocity.ToVector3() + firePoint.forward * projectileSpeed;
        if (tracerCounter >= tracerInterval)
        {
            if(projectileRB.TryGetComponent<TrailRenderer>(out var projectileTrail))
            {
                projectileTrail.enabled = true;
            }
            tracerCounter = 0;
        }
        if (shootParticles != null)
            Instantiate(shootParticles, firePoint.position, firePoint.rotation);

        if (casingPoint != null && casingPrefab != null)
        {
            Rigidbody casingRigidbody = Instantiate(casingPrefab, casingPoint.position, casingPoint.rotation).GetComponent<Rigidbody>();
            casingRigidbody.angularVelocity = Random.insideUnitSphere * casingRandomness;
            casingRigidbody.linearVelocity = turretSystem.ship.doubleRigidbody.velocity.ToVector3() + (casingPoint.up + Random.insideUnitSphere * casingRandomness) * casingSpeed;
        }
    }

    public bool GetObstruction(out RaycastHit hit)
    {
        return Physics.Raycast(firePoint.position, firePoint.forward, out hit, Mathf.Infinity, ~ignoreLayers);
    }

    public void TryPlayDamagedParticles()
    {
        if (damagedParticles != null && !damagedParticles.isPlaying && statSystem.health / statSystem.maxHealth < damagedThreshold)
        {
            damagedParticles.Play();
        }
    }

    public void OnDeath()
    {
        aliveGameObject.SetActive(false);
        destroyedGameObject.SetActive(true);
        destroyed = true;
    }

    public void Repair(float healAmount)
    {
        statSystem.Heal(healAmount);
        aliveGameObject.SetActive(true);
        destroyedGameObject.SetActive(false);
        TryPlayDamagedParticles();
        destroyed = false;
    }

    public void IgnoreCollider(Collider collider)
    {
        ignoreColliders.Add(collider);
    }

    public void AddTarget(RadarTarget target)
    {
        if (targetTagsSet.Contains(target.tag) && target.team != turretSystem.ship.team)
        {
            validTargets.Add(target.GetID());
        }
    }

    public void RemoveTarget(RadarTarget target)
    {
        if (targetTagsSet.Contains(target.tag))
        {
            validTargets.Remove(target.GetID());
        }
        if (validTargets.Count == 0)
        {
            currentTarget = null;
            aimDirection = Vector3.zero;
        }
    }
}
