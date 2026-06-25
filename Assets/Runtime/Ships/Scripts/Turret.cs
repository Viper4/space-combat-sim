using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;
using Random = UnityEngine.Random;
using FishNet.Object;
using FishNet.Connection;

[RequireComponent(typeof(StatSystem))]
public class Turret : NetworkBehaviour
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

    [SerializeField] private Transform casingPoint;
    [SerializeField] private float casingRandomness = 0.1f;
    [SerializeField] private GameObject casingPrefab;
    [SerializeField] private float casingSpeed = 5;

    private double sqrClosestDistance = double.MaxValue;
    private double fastestClosing = double.MinValue;
    private RadarTarget bestOffTarget = null;
    private RadarTarget bestDefTarget = null;
    public RadarTarget currentTarget;

    [SerializeField, Range(0, 1), Tooltip("0=only use velocity to estimate target arrival time, 1=acceleration dominant estimate of arrival time")] private float accelerationHeuristic = 0.5f;
    [SerializeField] private string explosiveTag;

    public float explosionRadius = 20f;

    [HideInInspector] public Vector3 aimDirection;
    public bool shoot {get; private set;} = false;
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
    private Queue<KeyValuePair<float, Vector3d>> predictions = new Queue<KeyValuePair<float, Vector3d>>();

    private void Start()
    {
        statSystem = GetComponent<StatSystem>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        turretSystem.StartTargetSearch += ResetTargetSearch;
        turretSystem.CheckTarget += CheckTarget;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        turretSystem.StartTargetSearch -= ResetTargetSearch;
        turretSystem.CheckTarget -= CheckTarget;
    }

    private void Update()
    {
        if (!active)
            return;
        if (!IsOffline && !IsOwner)
            return;
        
        if (test && currentTarget != null && predictions.Count > 0)
        {
            KeyValuePair<float, Vector3d> prediction = predictions.Peek();
            if (Time.time >= prediction.Key)
            {
                Vector3d realTargetPos = currentTarget.scaledRigidbody.scaledTransform.realPosition;
                Vector3d error = realTargetPos - prediction.Value;
                Debug.Log($"{Time.time - prediction.Key} Turret prediction: {prediction.Value}, actual: {realTargetPos}, error: {error}, error distance: {error.magnitude}");
                predictions.Dequeue();
            }
        }
        else
        {
            predictions.Clear();
        }
    }

    private void FixedUpdate()
    {
        if (!active)
            return;
        
        if (IsOffline || IsOwner)
        {
            if (currentTarget != null)
            {
                // Calculate aim direction needed to get a bullet fired at projectileSpeed to reach target's future position
                Vector3d realFirePoint = turretSystem.ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
                Vector3d realTargetPos = currentTarget.scaledRigidbody.scaledTransform.realPosition;
                Vector3d relativePosition = realTargetPos - realFirePoint;

                if (relativePosition.sqrMagnitude > turretSystem.detectRadius * turretSystem.detectRadius)
                {
                    currentTarget = null;
                    return;
                }

                Vector3d targetVelocity = currentTarget.scaledRigidbody.velocity;
                Vector3d relativeVelocity = targetVelocity - turretSystem.ship.scaledRigidbody.velocity;
                
                // Assume the bullet's acceleration after getting fired is only from gravity
                Vector3d projectileAcceleration = turretSystem.ship.scaledRigidbody.GetGravity();
                Vector3d relativeAcceleration = currentTarget.acceleration - projectileAcceleration;

                // Maybe add noise or something to bulletTime
                double bulletTime = SpaceMath.CalculateProjectileTime(relativePosition, relativeVelocity, relativeAcceleration, projectileSpeed);
                Vector3d predictedRelativePos = relativePosition
                        + (relativeVelocity * bulletTime)
                        + (0.5 * bulletTime * bulletTime * relativeAcceleration);
                Vector3d direction = predictedRelativePos.normalized;
                Vector3d simulatedVelocity = turretSystem.ship.scaledRigidbody.velocity + direction * projectileSpeed;
                Vector3d simulatedPos = realFirePoint + simulatedVelocity * bulletTime + 0.5 * bulletTime * bulletTime * projectileAcceleration;
                aimDirection = direction.ToVector3();

                var prediction = new KeyValuePair<float, Vector3d>(Time.time + (float)bulletTime, simulatedPos);
                predictions.Enqueue(prediction);

                if (!shoot && !turretSystem.manualControl && (aimDirection - firePoint.forward).sqrMagnitude < maxShootDelta * maxShootDelta)
                    shoot = true;
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

                if (!turretSystem.manualControl && currentTarget != null)
                {
                    if ((aimDirection - firePoint.forward).sqrMagnitude < maxShootDelta * maxShootDelta)
                    {
                        if (!shoot)
                        {
                            SetShoot(true);
                        }
                    }
                    else
                    {
                        if (shoot)
                        {
                            SetShoot(false);
                        }
                    }
                }
            }
        }

        if (!obstructed && shoot && turretSystem.currentAmmo > 0)
        {
            fireTime += Time.deltaTime;
            if (fireTime >= fireRate)
            {
                if (IsOffline)
                {
                    FireRealBullet();
                }
                else if (IsServerInitialized)
                {
                    NonOwnerFire();
                    FireRealBullet();
                }
                else if (IsOwner)
                {
                    // Fire visual bullet immediately to avoid perceived lag for owner client
                    FireVisualBullet();
                }
            }
        }
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
        if (!IsOffline && !IsOwner)
            return;
        Vector3d realFirePoint = turretSystem.ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
        Vector3d relativePosition = target.scaledRigidbody.scaledTransform.realPosition - realFirePoint;
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
            if (Physics.Raycast(origin.position, direction.ToVector3(), out RaycastHit hit, (float)distance, ~ignoreLayers, QueryTriggerInteraction.Ignore) && hit.transform != target.transform)
            {
                // Obstructed view
                return;
            }

            // Dont want to use closingVelocity and closingAcceleration since we only want to consider the intention of the target, not if the ship is moving towards it
            double incomingVelocity = -Vector3d.Dot(target.scaledRigidbody.velocity, direction);
            double incomingAcceleration = -Vector3d.Dot(target.acceleration, direction);

            double estimatedClosingTime = distance / (incomingVelocity + accelerationHeuristic * incomingAcceleration * distance);
            
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
                if (IsOffline)
                    HUDSystem.Instance.SetTurretsTargetingOffline(prevTarget.GetID(), prevTarget.turretsTargeting);
                else
                    HUDSystem.Instance.SetTurretsTargetingTargetRpc(Owner, prevTarget.GetID(), prevTarget.turretsTargeting);
            }
            if (currentTarget != null)
            {
                currentTarget.turretsTargeting++;
                if (IsOffline)
                    HUDSystem.Instance.SetTurretsTargetingOffline(prevTarget.GetID(), prevTarget.turretsTargeting);
                else
                    HUDSystem.Instance.SetTurretsTargetingTargetRpc(Owner, currentTarget.GetID(), currentTarget.turretsTargeting);
            }
        }
    }

    [ServerRpc]
    private void SetShootServerRpc(bool shoot)
    {
        this.shoot = shoot;
        if (shoot)
        {
            // Need to update obstructed bool on server
            GetRaycastHit(out _);
        }
        // Periodically synchronize ammo count with owner
        SetOwnerAmmoCountTargetRpc(Owner, turretSystem.currentAmmo);
    }

    [TargetRpc]
    private void SetOwnerAmmoCountTargetRpc(NetworkConnection conn, int ammo)
    {
        turretSystem.SetAmmo(ammo);
    }

    public void SetShoot(bool shoot)
    {
        if (this.shoot == shoot)
            return;
        this.shoot = shoot;
        if (shoot)
            GetRaycastHit(out _); // Update obstructed
        if (!IsOffline)
            SetShootServerRpc(shoot);
    }

    /// <summary>
    /// Shoot a bullet purely for visual effect. This is for non-server clients to maintain server authority for physics.
    /// </summary>
    private void FireVisualBullet()
    {
        ScaledRigidbody projectileRB = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation).GetComponent<ScaledRigidbody>();
        projectileRB.velocity = turretSystem.ship.scaledRigidbody.velocity + firePoint.forward.ToVector3d() * projectileSpeed;
        projectileRB.scaledTransform.realPosition = turretSystem.ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
        projectileRB.DestroyScaledColliders(); // Server takes authority over simulating physics

        Collider projectileCollider = projectileRB.GetComponent<Collider>();
        foreach(Collider collider in ignoreColliders)
        {
            Physics.IgnoreCollision(projectileCollider, collider, true);
        }
        
        if (projectileRB.TryGetComponent<TrailRenderer>(out var trailRenderer))
        {
            if (tracerCounter >= tracerInterval)
            {
                trailRenderer.enabled = true;
                tracerCounter = 0;
            }
            else
            {
                trailRenderer.enabled = false;
                tracerCounter++;
            }
        }
    }

    private void FireRealBullet()
    {
        fireTime = 0;
        turretSystem.OnTurretFire();
        ScaledRigidbody projectileRB = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation).GetComponent<ScaledRigidbody>();
        projectileRB.velocity = turretSystem.ship.scaledRigidbody.velocity + firePoint.forward.ToVector3d() * projectileSpeed;
        projectileRB.scaledTransform.realPosition = turretSystem.ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
        projectileRB.IgnoreScaledRigidbody(turretSystem.ship.scaledRigidbody, true);

        Collider projectileCollider = projectileRB.GetComponent<Collider>();
        foreach(Collider collider in ignoreColliders)
        {
            Physics.IgnoreCollision(projectileCollider, collider, true);
        }

        if (projectileRB.TryGetComponent<TrailRenderer>(out var trailRenderer))
        {
            if (tracerCounter >= tracerInterval)
            {
                trailRenderer.enabled = true;
                tracerCounter = 0;
            }
            else
            {
                trailRenderer.enabled = false;
                tracerCounter++;
            }
        }

        if (turretSystem.ship.scaledRigidbody.scaledTransform.visible && shootParticles != null)
        {
            Instantiate(shootParticles, firePoint.position, firePoint.rotation, transform).GetComponent<ScaledRigidbody>();
        }

        if (casingPoint != null && casingPrefab != null)
        {
            Vector3d realCasingPoint = turretSystem.ship.scaledRigidbody.scaledTransform.TransformRenderPoint(casingPoint.position);
            ScaledRigidbody casingRigidbody = Instantiate(casingPrefab, casingPoint.position, casingPoint.rotation).GetComponent<ScaledRigidbody>();
            casingRigidbody.scaledTransform.realPosition = realCasingPoint;
            casingRigidbody.angularVelocity = (Random.insideUnitSphere * casingRandomness).ToVector3d();
            casingRigidbody.velocity = turretSystem.ship.scaledRigidbody.velocity + ((casingPoint.up + Random.insideUnitSphere * casingRandomness) * casingSpeed).ToVector3d();
        }
    }

    [ObserversRpc(ExcludeOwner = true)]
    private void NonOwnerFire()
    {
        FireVisualBullet();
    }

    public bool GetRaycastHit(out RaycastHit hit)
    {
        bool gotHit = Physics.Raycast(origin.position, origin.forward, out hit, Mathf.Infinity, ~ignoreLayers, QueryTriggerInteraction.Ignore);
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
}
