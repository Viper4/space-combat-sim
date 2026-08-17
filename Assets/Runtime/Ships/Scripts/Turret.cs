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
    [Header("Parts")]
    public bool active = true;
    public StatSystem statSystem;
    [SerializeField] private TurretSystem turretSystem;
    [SerializeField] private Ship ship;
    [SerializeField] private Transform origin;
    public Transform platform;
    public Transform barrel;
    public Transform firePoint;

    [Header("Limits")]
    [SerializeField] protected LayerMask ignoreLayers;
    private float currentYaw;
    private float currentPitch;
    [SerializeField] private Vector3 minAngles;
    [SerializeField] private Vector3 maxAngles;
    [SerializeField] private float rotateSpeed = 180f;

    [Header("Shooting")]
    [SerializeField] private int maxAmmo;
    private int ammo;
    [SerializeField] private SliderIndicator ammoIndicator;
    [SerializeField] private float maxShootDelta = 0.05f;
    [SerializeField, Tooltip("One bullet per fireRate seconds.")] private float fireRate = 0.15f;
    private float nextFireTime = 0f;
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

    [Header("Targeting")]
    [SerializeField, Range(0, 1), Tooltip("0=only use velocity to estimate target arrival time, 1=acceleration dominant estimate of arrival time")] private float accelerationHeuristic = 0.5f;
    [SerializeField] private string explosiveTag;

    public float explosionRadius = 20f;

    [HideInInspector] public Vector3 aimDirection;
    public bool shoot {get; private set;} = false;
    [SerializeField] protected bool showLines;
    private bool obstructed = false;

    public GameObject UIModel;

    [Header("Destruction")]
    public bool destroyed = false;
    [SerializeField, Tooltip("Percent of health lost before enabling damaged particles.")] private float damagedThreshold = 0.5f;
    [SerializeField] private ParticleSystem damagedParticles;
    [SerializeField] private GameObject aliveGameObject;
    [SerializeField] private GameObject destroyedGameObject;

    private List<Collider> ignoreColliders = new List<Collider>();

    [SerializeField] private bool test;
    private Queue<KeyValuePair<float, Vector3d>> predictions = new Queue<KeyValuePair<float, Vector3d>>();

    [SerializeField] private bool overrideShoot = false;

    private bool IsOwnerOrOffline => IsOwner || IsOffline;

    private void Start()
    {
        statSystem = GetComponent<StatSystem>();
        if (IsOffline)
        {
            turretSystem.StartTargetSearch += ResetTargetSearch;
            turretSystem.CheckTarget += CheckTarget;
        }
        SetCurrentAmmo(maxAmmo);
    }

    private void OnDestroy()
    {
        if (!IsOwnerOrOffline)
            return;
        turretSystem.StartTargetSearch -= ResetTargetSearch;
        turretSystem.CheckTarget -= CheckTarget;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            return;
        turretSystem.StartTargetSearch += ResetTargetSearch;
        turretSystem.CheckTarget += CheckTarget;
    }

    private void Update()
    {
        if (!active || !ship.isStarted)
            return;
        if (!IsOwnerOrOffline)
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
        if (IsServerInitialized && !IsOwner)
            CheckFire(); // Owner controls rotation of turret

        if (!active || !ship.isStarted || !IsOwnerOrOffline)
            return;

        if (currentTarget != null)
        {
            if (!currentTarget.passivelyDetected && !currentTarget.activelyDetected)
            {
                currentTarget = null;
                return;
            }

            // Calculate aim direction needed to get a bullet fired at projectileSpeed to reach target's future position
            Vector3d realFirePoint = ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
            Vector3d realTargetPos = currentTarget.scaledRigidbody.scaledTransform.realPosition;
            Vector3d relativePosition = realTargetPos - realFirePoint;

            Vector3d targetVelocity = currentTarget.scaledRigidbody.velocity;
            Vector3d relativeVelocity = targetVelocity - ship.scaledRigidbody.velocity;
            
            // Assume the bullet's acceleration after getting fired is only from gravity
            Vector3d projectileAcceleration = ship.scaledRigidbody.GetGravity();
            Vector3d relativeAcceleration = currentTarget.scaledRigidbody.acceleration - projectileAcceleration;

            // Maybe add noise or something to bulletTime
            double bulletTime = SpaceMath.CalculateProjectileTime(relativePosition, relativeVelocity, relativeAcceleration, projectileSpeed);
            Vector3d predictedRelativePos = relativePosition
                    + (relativeVelocity * bulletTime)
                    + (0.5 * bulletTime * bulletTime * relativeAcceleration);
            Vector3d direction = predictedRelativePos.normalized;
            Vector3d simulatedVelocity = ship.scaledRigidbody.velocity + direction * projectileSpeed;
            Vector3d simulatedPos = realFirePoint + simulatedVelocity * bulletTime + 0.5 * bulletTime * bulletTime * projectileAcceleration;
            aimDirection = direction.ToVector3();

            var prediction = new KeyValuePair<float, Vector3d>(Time.time + (float)bulletTime, simulatedPos);
            predictions.Enqueue(prediction);

            if (!shoot && !turretSystem.manualControl && (aimDirection - firePoint.forward).sqrMagnitude < maxShootDelta * maxShootDelta)
                shoot = true;
        }

        bool hasAimDirection = aimDirection.sqrMagnitude > 0.0001f;
        if (hasAimDirection)
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
        }

        if (overrideShoot)
        {
            SetShoot(true);
        }
        else if (!turretSystem.manualControl)
        {
            if (currentTarget != null && hasAimDirection && (aimDirection - firePoint.forward).sqrMagnitude < maxShootDelta * maxShootDelta)
            {
                SetShoot(true);
            }
            else
            {
                SetShoot(false);
            }
        }

        CheckFire();
    }

    private void CheckFire()
    {
        if (Time.fixedTime < nextFireTime)
            return;
        if (!obstructed && shoot && ammo > 0)
        {
            if (IsOffline)
            {
                FireRealBullet();
            }
            else if (IsServerInitialized)
            {
                FireVisualBulletObserversRpc();
                FireRealBullet();
            }
            else if (IsOwner)
            {
                // Fire visual bullet immediately to avoid perceived lag for owner client
                FireVisualBullet();
            }
        }
        nextFireTime += fireRate;
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
        if (!IsOwnerOrOffline)
            return;
        if (!target.passivelyDetected && !target.activelyDetected)
            return;
        Vector3d realFirePoint = ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
        Vector3d relativePosition = target.scaledRigidbody.scaledTransform.realPosition - realFirePoint;
        double distance = relativePosition.magnitude;
        Vector3d direction = relativePosition / distance;
        if (Physics.Raycast(origin.position, direction.ToVector3(), out RaycastHit hit, (float)distance, ~ignoreLayers, QueryTriggerInteraction.Ignore) && hit.transform != target.transform)
        {
            // Obstructed view
            return;
        }
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
            // Dont want to use closingVelocity and closingAcceleration since we only want to consider the intention of the target, not if the ship is moving towards it
            double incomingVelocity = -Vector3d.Dot(target.scaledRigidbody.velocity, direction);
            double incomingAcceleration = -Vector3d.Dot(target.scaledRigidbody.acceleration, direction);

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
                HUDSystem.Instance.SetTurretsTargeting(prevTarget.GetID(), prevTarget.turretsTargeting);
            }
            if (currentTarget != null)
            {
                currentTarget.turretsTargeting++;
                HUDSystem.Instance.SetTurretsTargeting(currentTarget.GetID(), currentTarget.turretsTargeting);
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
    }

    [TargetRpc]
    private void SetOwnerAmmoCountTargetRpc(NetworkConnection conn, int ammo)
    {
        this.ammo = ammo;
        if (ammoIndicator != null)
            ammoIndicator.UpdateUI(ammo, maxAmmo);
    }

    public void SetShoot(bool shoot)
    {
        if (!IsOwnerOrOffline)
            return;
        if (this.shoot == shoot)
            return;
        this.shoot = shoot;
        if (shoot)
            GetRaycastHit(out _); // Update obstructed
        if (IsOwner)
            SetShootServerRpc(shoot);
    }

    /// <summary>
    /// Shoot a bullet purely for visual effect. This is for non-server clients to maintain server authority for physics.
    /// </summary>
    private void FireVisualBullet()
    {
        Vector3d realBulletPoint = ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
        // Retarded hack needed to prevent ScaledTransform from running Awake() and overriding transform.position with a zero Vector realPosition
        firePoint.gameObject.SetActive(false);
        ScaledRigidbody projectileRB = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation, firePoint).GetComponent<ScaledRigidbody>();
        ScaledTransform projectileScaledTransform = projectileRB.GetComponent<ScaledTransform>();
        projectileScaledTransform.realPosition = realBulletPoint;
        
        firePoint.gameObject.SetActive(true);
        projectileRB.transform.SetParent(null);
        // These methods need ScaledRigidbody to be initialized, so we call Awake() by setting it active before calling these
        projectileRB.velocity = ship.scaledRigidbody.velocity + (firePoint.forward * projectileSpeed).ToVector3d();
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
        ammo--;
        Vector3d realBulletPoint = ship.scaledRigidbody.scaledTransform.TransformRenderPoint(firePoint.position);
        // Retarded hack needed to prevent ScaledTransform from running Awake() and overriding transform.position with a zero Vector realPosition
        firePoint.gameObject.SetActive(false);
        ScaledRigidbody projectileRB = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation, firePoint).GetComponent<ScaledRigidbody>();
        ScaledTransform projectileScaledTransform = projectileRB.GetComponent<ScaledTransform>();
        projectileScaledTransform.realPosition = realBulletPoint;

        firePoint.gameObject.SetActive(true);
        projectileRB.transform.SetParent(null);
        // These methods need ScaledRigidbody to be initialized, so we call Awake() by setting it active before calling these
        projectileRB.velocity = ship.scaledRigidbody.velocity + (firePoint.forward * projectileSpeed).ToVector3d();
        projectileRB.IgnoreScaledRigidbody(ship.scaledRigidbody, true);

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

        if (ship.scaledRigidbody.scaledTransform.visible && shootParticles != null)
        {
            Instantiate(shootParticles, firePoint.position, firePoint.rotation, transform).GetComponent<ScaledRigidbody>();
        }

        if (casingPoint != null && casingPrefab != null)
        {
            Vector3d realCasingPoint = ship.scaledRigidbody.scaledTransform.TransformRenderPoint(casingPoint.position);
            ScaledRigidbody casingRigidbody = Instantiate(casingPrefab, casingPoint.position, casingPoint.rotation).GetComponent<ScaledRigidbody>();
            casingRigidbody.scaledTransform.realPosition = realCasingPoint;
            casingRigidbody.angularVelocity = Random.insideUnitSphere * casingRandomness;
            casingRigidbody.velocity = ship.scaledRigidbody.velocity + ((casingPoint.up + Random.insideUnitSphere * casingRandomness) * casingSpeed).ToVector3d();
        }

        if (IsOwnerOrOffline)
        {
            if (ammoIndicator != null)
                ammoIndicator.UpdateUI(ammo, maxAmmo);
        }
        else if (IsServerInitialized)
        {
            // Synchronize ammo count with owner
            SetOwnerAmmoCountTargetRpc(Owner, ammo);
        }
    }

    [ObserversRpc(ExcludeServer = true)]
    private void FireVisualBulletObserversRpc()
    {
        FireVisualBullet();
    }

    public bool GetRaycastHit(out RaycastHit hit)
    {
        bool gotHit = Physics.Raycast(origin.position, origin.forward, out hit, Mathf.Infinity, ~ignoreLayers, QueryTriggerInteraction.Ignore);
        obstructed = gotHit && hit.transform == ship.transform;
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

    public void SetFireOffset(int index, int turretCount)
    {
        nextFireTime = Time.fixedTime + index * fireRate / turretCount;
    }

    public int GetCurrentAmmo()
    {
        return ammo;
    }

    public void SetCurrentAmmo(int value)
    {
        ammo = Mathf.Clamp(value, 0, maxAmmo);
    }

    public int GetMaxAmmo()
    {
        return maxAmmo;
    }
}
