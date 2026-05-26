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
    [SerializeField] protected Transform origin;
    [SerializeField] public Transform platform;
    [SerializeField] public Transform barrel;
    public Transform firePoint;
    [SerializeField] private Transform casingPoint;
    [SerializeField] private float casingRandomness = 0.1f;

    [SerializeField] private Vector3 minAngles;
    [SerializeField] private Vector3 maxAngles;
    [SerializeField] private float rotateSpeed = 180f;

    [SerializeField] private float shootAngle = 1;
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
    private Vector3 futureTargetPosition;

    [SerializeField] private string[] targetTags;
    [SerializeField] private string explosiveTag;
    private HashSet<string> targetTagsSet = new HashSet<string>();

    private List<RadarTarget> validTargets = new List<RadarTarget>();

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
                    if (Vector3.Angle(firePoint.forward, aimDirection) < shootAngle)
                    {
                        Fire();
                    }
                }
            }
            if (Mathf.Abs(aimDirection.x) > 0.01f || Mathf.Abs(aimDirection.y) > 0.01f || Mathf.Abs(aimDirection.z) > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(aimDirection, platform.up);
                platform.rotation = barrel.rotation = Quaternion.RotateTowards(barrel.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                platform.localEulerAngles = new Vector3(0, platform.localEulerAngles.y, 0);
                barrel.localEulerAngles = CustomMethods.Clamp(barrel.localEulerAngles.NormalizeEulerAngles(), minAngles, maxAngles);
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

                    // Calculate future position of target
                    Vector3 relativePosition = currentTarget.transform.position - origin.position;
                    Vector3 relativeVelocity = currentTarget.velocity - turretSystem.ship.doubleRigidbody.velocity.ToVector3();
                    Vector3 relativeAcceleration = currentTarget.acceleration - turretSystem.ship.acceleration;
                    float distance = relativePosition.magnitude;
                    float predictionTime = distance / projectileSpeed;
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 predictedRelativePosition = relativePosition + (relativeVelocity * predictionTime) + (0.5f * predictionTime * predictionTime * relativeAcceleration);
                        predictionTime = predictedRelativePosition.magnitude / projectileSpeed;
                    }

                    futureTargetPosition = currentTarget.transform.position + (relativeVelocity * predictionTime) + (0.5f * predictionTime * predictionTime * relativeAcceleration);

                    aimDirection = (futureTargetPosition - origin.position).normalized;

                    if (!Physics.Raycast(firePoint.position, aimDirection, out RaycastHit hit, distance - 0.1f, ~ignoreLayers) || hit.rigidbody == currentTarget.attachedRB)
                        fire = true;

                    if (showLines)
                    {
                        Debug.DrawLine(firePoint.position, futureTargetPosition, Color.green, Time.fixedDeltaTime);
                    }
                }
            }
        }
    }

    private void SelectTarget()
    {
        RadarTarget bestTarget = previousTarget;
        float fastestClosingSpeed = float.MaxValue;
        for (int i = validTargets.Count - 1; i >= 0; i--)
        {
            if (validTargets[i] == null)
            {
                validTargets.RemoveAt(i);
                continue;
            }

            Vector3 direction = (validTargets[i].transform.position - barrel.position).normalized;
            if (Physics.Raycast(barrel.position, direction, out RaycastHit hit, 10f, ~ignoreLayers) && hit.rigidbody != validTargets[i])
                continue; // Obstructed view of target

            Vector3 relativeVelocity = validTargets[i].velocity - turretSystem.ship.doubleRigidbody.velocity.ToVector3();

            float closingSpeed = Vector3.Dot(relativeVelocity, direction); // Negative value means target is moving towards us
            if (closingSpeed > projectileSpeed)
                continue; // Target is moving away too fast

            float sqrDst = (validTargets[i].transform.position - barrel.position).sqrMagnitude;
            if (closingSpeed < fastestClosingSpeed && (sqrDst > explosionRadius * explosionRadius || !validTargets[i].transform.CompareTag(explosiveTag)))
            {
                bestTarget = validTargets[i];
                fastestClosingSpeed = closingSpeed;
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
            validTargets.Add(target);
        }
    }

    public void RemoveTarget(RadarTarget target)
    {
        if (targetTagsSet.Contains(target.tag))
        {
            validTargets.Remove(target);
        }
        if (validTargets.Count == 0)
        {
            currentTarget = null;
            aimDirection = Vector3.zero;
        }
    }
}
