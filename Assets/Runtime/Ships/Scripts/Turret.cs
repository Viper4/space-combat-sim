using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;
using Random = UnityEngine.Random;

public class Turret : MonoBehaviour
{
    [Header("Turret")] public StatSystem statSystem;
    [SerializeField] private TurretSystem turretSystem;

    public bool active = true;
    [SerializeField] protected LayerMask ignoreLayers;
    private float currentYaw;
    private float currentPitch;
    [SerializeField] private Transform origin;
    public Transform platform;
    public Transform barrel;
    public Transform firePoint;
    private double firePointOffset;
    [SerializeField] private Transform casingPoint;
    [SerializeField] private float casingRandomness = 0.1f;

    [SerializeField] private Vector3 minAngles;
    [SerializeField] private Vector3 maxAngles;
    [SerializeField] private float rotateSpeed = 180f;

    [SerializeField] private float maxShootDelta = 0.05f;
    [SerializeField, Tooltip("One bullet per fireRate seconds.")] private float fireRate = 0.15f;
    [SerializeField] protected float fireTime = 0;
    [SerializeField] protected GameObject projectilePrefab;
    [SerializeField] private GameObject shootParticles;
    [SerializeField] protected float projectileSpeed = 50;
    [SerializeField, Tooltip("0 for no tracer. Otherwise 1 tracer every tracerInterval shots.")] protected int tracerInterval = 3;
    protected int tracerCounter = 0;

    [SerializeField] private GameObject casingPrefab;
    [SerializeField] private float casingSpeed = 5;

    private double sqrClosestDistance = double.MaxValue;
    private double fastestClosing = double.MinValue;
    private RadarTarget bestOffTarget = null;
    private RadarTarget bestDefTarget = null;
    public RadarTarget currentTarget;

    [SerializeField, Range(0, 1), Tooltip("0=only use velocity to estimate target arrival time, 1=acceleration dominant estimate of arrival time")] private float accelerationHeuristic = 0.5f;
    [SerializeField] private int predictionIterations = 4;
    [SerializeField] private string explosiveTag;

    public float explosionRadius = 20f;

    [HideInInspector] public Vector3 aimDirection;
    [HideInInspector] public bool shoot = false;
    [SerializeField] protected bool showLines;
    private bool obstructed = false;

    public GameObject UIModel;

    public bool destroyed = false;
    [SerializeField, Tooltip("Percent of health lost before enabling damaged particles.")] private float damagedThreshold = 0.5f;
    [SerializeField] private ParticleSystem damagedParticles;
    [SerializeField] private GameObject aliveGameObject;
    [SerializeField] private GameObject destroyedGameObject;

    private List<Collider> ignoreColliders = new List<Collider>();

    [SerializeField] private bool test;
    Queue<KeyValuePair<float, Vector3d>> predictions = new Queue<KeyValuePair<float, Vector3d>>();

    protected virtual void Start()
    {
        statSystem = GetComponent<StatSystem>();
        turretSystem.StartTargetSearch += ResetTargetSearch;
        turretSystem.CheckTarget += CheckTarget;
        firePointOffset = (origin.position - firePoint.position).magnitude;
        if (turretSystem.ship.doubleRigidbody.scaledTransform.inScaledSpace)
            firePointOffset *= turretSystem.ship.doubleRigidbody.scaledTransform.scaleFactor;
    }

    protected virtual void Update()
    {
        if (!active)
            return;
        
        if (test && currentTarget != null && predictions.Count > 0)
        {
            KeyValuePair<float, Vector3d> prediction = predictions.Peek();
            if (Time.time >= prediction.Key)
            {
                Vector3d realTargetPos = currentTarget.doubleRigidbody.scaledTransform.realPosition;
                Vector3d error = realTargetPos - prediction.Value;
                Debug.Log($"Turret prediction: {prediction.Value}, actual: {realTargetPos}, error: {error}, err magnitude: {error.magnitude}");
                predictions.Dequeue();
            }
        }
        else
        {
            predictions.Clear();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!active)
            return;
        if (!turretSystem.manualControl)
        {
            shoot = false;

            if (currentTarget != null)
            {
                // Calculate future position of target
                Vector3d realFirePoint = GetRealFirePoint();
                Vector3d realTargetPos = currentTarget.doubleRigidbody.scaledTransform.realPosition;
                Vector3d relativePosition = realTargetPos - realFirePoint;

                Vector3d targetVelocity = currentTarget.doubleRigidbody.velocity;
                Vector3d relativeVelocity = targetVelocity - turretSystem.ship.doubleRigidbody.velocity;
                
                // Assume the bullet's acceleration after getting fired is only from gravity
                Vector3d projectileAcceleration = turretSystem.ship.doubleRigidbody.GetGravity();
                Vector3d relativeAcceleration = currentTarget.acceleration - projectileAcceleration;

                double bulletTime = SpaceMath.CalculateProjectileTime(relativePosition, relativeVelocity, relativeAcceleration, projectileSpeed);
                Vector3d predictedRelativePos = relativePosition
                        + (relativeVelocity * bulletTime)
                        + (0.5 * bulletTime * bulletTime * relativeAcceleration);
                Vector3d direction = predictedRelativePos.normalized;
                Vector3d simulatedVelocity = turretSystem.ship.doubleRigidbody.velocity + direction * projectileSpeed;
                Vector3d simulatedPos = realFirePoint + simulatedVelocity * bulletTime + 0.5 * bulletTime * bulletTime * projectileAcceleration;
                aimDirection = direction.ToVector3();

                var prediction = new KeyValuePair<float, Vector3d>(Time.time + (float)bulletTime, simulatedPos);
                predictions.Enqueue(prediction);

                if ((aimDirection - firePoint.forward).sqrMagnitude < maxShootDelta * maxShootDelta)
                    shoot = true;
            }
        }

        if (aimDirection.sqrMagnitude > 0.00001f)
        {
            Vector3 localDir = platform.parent.InverseTransformDirection(aimDirection);
            float targetYaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            targetYaw = Mathf.Clamp(SpaceMath.NormalizeAngle(targetYaw), minAngles.y, maxAngles.y);

            Quaternion yawRotation = Quaternion.Euler(0f, targetYaw, 0f);
            Vector3 yawSpaceDir = Quaternion.Inverse(yawRotation) * localDir;
            float targetPitch = -Mathf.Atan2(yawSpaceDir.y, yawSpaceDir.z) * Mathf.Rad2Deg;
            targetPitch = Mathf.Clamp(SpaceMath.NormalizeAngle(targetPitch), minAngles.x, maxAngles.x);

            currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, rotateSpeed * Time.fixedDeltaTime);
            currentPitch = Mathf.MoveTowardsAngle(currentPitch, targetPitch, rotateSpeed * Time.fixedDeltaTime);

            platform.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
            barrel.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

            Debug.DrawRay(origin.position, aimDirection * 1000f, Color.red, Time.fixedDeltaTime);
            Debug.DrawRay(origin.position, origin.forward * 1000f, Color.yellow, Time.fixedDeltaTime);
        }

        if (!obstructed && shoot)
        {
            fireTime += Time.deltaTime;
            if (fireTime >= fireRate)
            {
                Fire();
            }
        }
    }

    private Vector3d GetRealFirePoint()
    {
        Vector3d originOffset = (turretSystem.ship.transform.position - firePoint.position).ToVector3d();
        if (turretSystem.ship.doubleRigidbody.scaledTransform.inScaledSpace)
            originOffset *= turretSystem.ship.doubleRigidbody.scaledTransform.scaleFactor;
        return turretSystem.ship.doubleRigidbody.scaledTransform.realPosition - originOffset;
    }

    private void ResetTargetSearch()
    {
        fastestClosing = double.MaxValue;
        sqrClosestDistance = double.MaxValue;
        bestOffTarget = null;
        bestDefTarget = null;
    }

    private void CheckTarget(RadarTarget target, bool inKillRadius)
    {
        Vector3d realFirePoint = GetRealFirePoint();
        Vector3d relativePosition = target.doubleRigidbody.scaledTransform.realPosition - realFirePoint;
        if (turretSystem.IsOffensive(target))
        {
            // Use offensive strategy against target
            double sqrDistance = relativePosition.sqrMagnitude;
            if (sqrDistance < sqrClosestDistance)
            {
                bestOffTarget = target;
                sqrClosestDistance = sqrDistance;
            }
        }
        else if (turretSystem.IsDefensive(target))
        {
            // Use defensive strategy against target
            double distance = relativePosition.magnitude;
            Vector3d direction = relativePosition / distance;
            if (Physics.Raycast(origin.position, direction.ToVector3(), out RaycastHit hit, (float)distance, ~ignoreLayers) && hit.transform != target.transform)
            {
                // Obstructed view
                return;
            }

            double incomingSpeed = -Vector3d.Dot(target.doubleRigidbody.velocity, direction);
            double incomingAcceleration = -Vector3d.Dot(target.acceleration, direction);

            double estimatedClosingTime = distance / (incomingSpeed + accelerationHeuristic * incomingAcceleration * distance);
            
            if (!inKillRadius && estimatedClosingTime < 0)
            {
                // Ignore since it's not coming at us
                return;
            }

            if (estimatedClosingTime < fastestClosing && (distance > explosionRadius || !target.CompareTag(explosiveTag)))
            {
                bestDefTarget = target;
                fastestClosing = estimatedClosingTime;
            }
        }
        RadarTarget prevTarget = currentTarget;
        if (bestDefTarget != null)
        {
            currentTarget = bestDefTarget;
        }
        else if (bestOffTarget != null)
        {
            currentTarget = bestOffTarget;
        }
        else
        {
            currentTarget = null;
        }

        if (prevTarget != currentTarget)
        {
            // Update HUD Object's turretsTargeting metric
            if (prevTarget != null)
            {
                prevTarget.turretsTargeting--;
                turretSystem.UpdateHudForTarget(prevTarget);
            }
            if (currentTarget != null)
            {
                currentTarget.turretsTargeting++;
                turretSystem.UpdateHudForTarget(currentTarget);
            }
        }
    }

    protected virtual void Fire()
    {
        tracerCounter++;
        fireTime = 0;
        DoubleRigidbody projectileRB = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation).GetComponent<DoubleRigidbody>();
        Collider projectileCollider = projectileRB.GetComponent<Collider>();
        projectileRB.IgnoreDoubleRigidbody(turretSystem.ship.doubleRigidbody.id, true);
        foreach(Collider collider in ignoreColliders)
        {
            Physics.IgnoreCollision(projectileCollider, collider, true);
        }
        projectileRB.velocity = turretSystem.ship.doubleRigidbody.velocity + firePoint.forward.ToVector3d() * projectileSpeed;

        if (tracerCounter >= tracerInterval)
        {
            if(projectileRB.TryGetComponent<TrailRenderer>(out var projectileTrail))
            {
                projectileTrail.enabled = true;
            }
            tracerCounter = 0;
        }

        Vector3d offset = (firePoint.position - turretSystem.transform.position).ToVector3d();
        projectileRB.scaledTransform.realPosition = turretSystem.ship.doubleRigidbody.scaledTransform.realPosition + offset;

        if (!turretSystem.ship.doubleRigidbody.scaledTransform.inScaledSpace)
        {
            if (shootParticles != null)
                Instantiate(shootParticles, firePoint.position, firePoint.rotation, transform);

            if (casingPoint != null && casingPrefab != null)
            {
                Rigidbody casingRigidbody = Instantiate(casingPrefab, casingPoint.position, casingPoint.rotation).GetComponent<Rigidbody>();
                casingRigidbody.angularVelocity = Random.insideUnitSphere * casingRandomness;
                casingRigidbody.linearVelocity = turretSystem.ship.doubleRigidbody.velocity.ToVector3() + (casingPoint.up + Random.insideUnitSphere * casingRandomness) * casingSpeed;
            }
        }
    }

    public bool GetRaycastHit(out RaycastHit hit)
    {
        bool gotHit = Physics.Raycast(origin.position, origin.forward, out hit, Mathf.Infinity, ~ignoreLayers);
        obstructed = gotHit && hit.transform == turretSystem.ship.transform;
        return gotHit;
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

    public void AddIgnoredCollider(Collider collider)
    {
        ignoreColliders.Add(collider);
    }

    public void SetFireTime(float percentMax)
    {
        fireTime = percentMax * fireRate;
    }

    private void OnDestroy()
    {
        turretSystem.StartTargetSearch -= ResetTargetSearch;
        turretSystem.CheckTarget -= CheckTarget;
    }
}
