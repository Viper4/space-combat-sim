using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;
using FishNet.Object;

[RequireComponent(typeof(ScaledRigidbody), typeof(RadarTarget))]
public class Torpedo : NetworkBehaviour
{
    private ScaledRigidbody scaledRigidbody;
    private RadarTarget thisRadarTarget;

    [Header("Torpedo")]
    [SerializeField] private bool active;
    private bool canThrust;
    [SerializeField] private float targetEmissionRadius = 2000;
    [SerializeField] private float idleEmissionRadius = 250;
    [SerializeField] private CapsuleCollider _collider;
    [SerializeField] private ScaledCollider detonateTrigger;
    [SerializeField] private float engineForce = 10000f;
    [SerializeField] private float thrusterForce = 100f;

    [SerializeField] private float proportionalGain = 16.0f;
    [SerializeField] private float integralGain = 0.0f;
    [SerializeField] private float derivativeGain = 4.0f;
    private PIDController xPID;
    private PIDController yPID;
    private PIDController zPID;

    [SerializeField] private GameObject rocketTrail;

    [SerializeField, Tooltip("Collisions with a relative speed above this will detonate the torpedo.")] private float collideSpeedThreshold = 100f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionRadius = 15f;
    [SerializeField] private float explosionForce = 100f;
    [SerializeField] private float minDamage = 25f;
    [SerializeField] private float maxDamage = 75f;
    
    [SerializeField] private LayerMask ignoreLayers;
    [SerializeField] private RadarTarget target;
    [SerializeField] private float navigationConstant = 4f;
    [SerializeField] private float thrusterTorque;
    private float engineAcceleration;

    // Need to use this to prevent stack overflows
    private bool detonating = false;

    private bool IsServerOrOffline => IsServerInitialized || IsOffline;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        thisRadarTarget = GetComponent<RadarTarget>();
        thrusterTorque = _collider.height * thrusterForce;
        engineAcceleration = (float)(engineForce / scaledRigidbody.mass);
        if (IsOffline)
        {
            scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
            scaledRigidbody.OnScaledTriggerEnter += OnScaledTrigger;
        }

        xPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        yPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        zPID = new PIDController(proportionalGain, integralGain, derivativeGain);
    }

    private void OnDestroy()
    {
        scaledRigidbody.OnScaledCollisionEnter -= OnScaledCollide;
        scaledRigidbody.OnScaledTriggerEnter -= OnScaledTrigger;
    }


    public override void OnStartServer()
    {
        base.OnStartServer();
        
        scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
        scaledRigidbody.OnScaledTriggerEnter += OnScaledTrigger;
    }

    private void FixedUpdate()
    {
        if (!active || target == null)
        {
            SetRocketTrailActive(false);
            return;
        }

        Vector3d targetVelocity = target.scaledRigidbody.velocity;
        Vector3d torpedoVelocity = scaledRigidbody.velocity;
        Vector3d realTargetPosition = target.scaledRigidbody.scaledTransform.realPosition;
        Vector3d realTorpedoPosition = scaledRigidbody.scaledTransform.realPosition;

        Vector3d relativePosition = realTargetPosition - realTorpedoPosition;
        Vector3d relativeVelocity = targetVelocity - torpedoVelocity;
        double sqrDistance = relativePosition.sqrMagnitude;
        Vector3d targetDir = relativePosition / Math.Sqrt(sqrDistance);

        // Proportional Navigation Guidance to steer torpedo: a = cross(NV_r, omega)
        // omega = cross(R, V_r) / dot(R, R)
        // R = relative position
        Vector3d rotationVector = Vector3d.Cross(relativePosition, relativeVelocity) / sqrDistance;
        Vector3d pnAcceleration = Vector3d.Cross(navigationConstant * relativeVelocity, rotationVector);

        // How perpendicular is the torpedo's velocity to the target direction?
        // 0 = heading straight at target, 1 = fully sideways (orbiting)
        Vector3d velocityDir = torpedoVelocity.normalized;
        double perpendicularFactor = 1.0 - Math.Abs(Vector3d.Dot(velocityDir, targetDir));

        // Blend a braking force into the acceleration, scaled by how sideways we are
        Vector3d brakeAcceleration = -velocityDir * engineAcceleration * perpendicularFactor;
        Vector3d desiredAcceleration = targetDir * engineAcceleration + pnAcceleration + brakeAcceleration;

        Vector3d desiredForce = desiredAcceleration * scaledRigidbody.mass;
        if (canThrust)
        {
            Vector3 localDesiredForce = transform.InverseTransformDirection(desiredForce.ToVector3());
        
            // Find maximum force in desiredForce direction while maintaining thrust limits
            float scale = float.PositiveInfinity;

            // X thruster
            float xMagnitude = Mathf.Abs(localDesiredForce.x);
            if (xMagnitude > 0f)
            {
                scale = Mathf.Min(scale, thrusterForce / xMagnitude);
            }

            // Y thruster
            float yMagnitude = Mathf.Abs(localDesiredForce.y);
            if (yMagnitude > 0f)
            {
                scale = Mathf.Min(scale, thrusterForce / yMagnitude);
            }

            // Z engine/thruster
            float zLimit = localDesiredForce.z >= 0f ? engineForce : thrusterForce;

            float zMagnitude = Mathf.Abs(localDesiredForce.z);
            if (zMagnitude > 0f)
            {
                scale = Mathf.Min(scale, zLimit / zMagnitude);
            }

            Vector3 localForce = localDesiredForce * scale;

            scaledRigidbody.AddRelativeForce(localForce.ToVector3d(), ForceMode.Force);

            // Z axis stays the same — per-axis clamping is correct here
            if (localForce.z >= 0.0f)
            {
                SetRocketTrailActive(true);
                float trailScale = localForce.z / engineForce;
                rocketTrail.transform.localScale = Vector3.one * trailScale;
            }
            else
            {
                SetRocketTrailActive(false);
            }
        }
        else
        {
            SetRocketTrailActive(false);
        }

        // Rotate torpedo to align with desired force
        if (desiredForce.sqrMagnitude > 0.0001)
        {
            Vector3 desiredForward = desiredForce.normalized.ToVector3();

            Debug.DrawRay(transform.position, desiredForce.ToVector3(), Color.green);
            
            Vector3 worldRotationError = Vector3.Cross(transform.forward, desiredForward);

            Vector3 torque = new Vector3(
                xPID.GetOutput(worldRotationError.x, scaledRigidbody.angularVelocity.x, Time.fixedDeltaTime),
                yPID.GetOutput(worldRotationError.y, scaledRigidbody.angularVelocity.y, Time.fixedDeltaTime),
                zPID.GetOutput(worldRotationError.z, scaledRigidbody.angularVelocity.z, Time.fixedDeltaTime)
            );

            torque = Vector3.ClampMagnitude(torque, thrusterTorque);

            scaledRigidbody.AddTorque(torque, ForceMode.Force);
        }
    }

    [ObserversRpc(ExcludeServer = true, BufferLast = true)]
    private void SetRocketTrailActiveObserversRpc(bool active)
    {
        if (rocketTrail.activeSelf != active)
            rocketTrail.SetActive(active);
    }

    private void SetRocketTrailActive(bool active)
    {
        if(rocketTrail.activeSelf != active)
        {
            rocketTrail.SetActive(active);
            if (IsServerInitialized)
                SetRocketTrailActiveObserversRpc(active);
        }
    }

    public void Activate(RadarTarget target, float delay)
    {
        if (!IsServerOrOffline)
            return;
        this.target = target;
        StartCoroutine(ActivateRoutine(delay));
    }

    private IEnumerator ActivateRoutine(float delay)
    {
        active = true;
        yield return new WaitForSeconds(delay);
        canThrust = true;
        // _collider.enabled = true;
        // scaledRigidbody.EnableScaledColliders(true);
        if (target == null)
        {
            thisRadarTarget.SetEmissionTriggerRadius(idleEmissionRadius);
        }
        else
        {
            thisRadarTarget.SetEmissionTriggerRadius(targetEmissionRadius);
            if (thisRadarTarget.alertWhenTargeting && target.attachedRadar != null)
                target.attachedRadar.IncrementMissileLock(1);
        }
        thisRadarTarget.SetEmissionActive(true);
    }

    public void SetTarget(RadarTarget newTarget)
    {
        if (!IsServerOrOffline)
            return;
        if (thisRadarTarget.alertWhenTargeting && target != null && target.attachedRadar != null)
            target.attachedRadar.IncrementMissileLock(-1);
        target = newTarget;
        if (target == null)
        {
            thisRadarTarget.SetEmissionTriggerRadius(idleEmissionRadius);
        }
        else
        {
            thisRadarTarget.SetEmissionTriggerRadius(targetEmissionRadius);
            
            if (thisRadarTarget.alertWhenTargeting && target.attachedRadar != null)
                target.attachedRadar.IncrementMissileLock(1);
        }

        if (target == thisRadarTarget)
        {
            Debug.Log(GameLog.ObjectLog(this, "Self destruct detonate."));
            Detonate(null);
        }
    }

    private float CalculateDamage(float sqrDistance)
    {
        // y = -(maxHeight / intercept^2) * x^2 + maxHeight + minHeight;
        return Mathf.Max(-(maxDamage / (explosionRadius * explosionRadius)) * sqrDistance + maxDamage + minDamage, 0f);
    }

    private void InstantiateExplosion(Vector3d position, Vector3d hitVelocity, double hitMass)
    {
        if (scaledRigidbody.scaledTransform.visible)
        {
            ScaledTransform explosion = Instantiate(explosionPrefab, transform.position, transform.rotation).GetComponent<ScaledTransform>();
            if (explosion.TryGetComponent<ScaledRigidbody>(out var explosionRB))
            {
                explosionRB.velocity = Vector3d.Lerp(hitVelocity, scaledRigidbody.velocity, scaledRigidbody.mass / (scaledRigidbody.mass + hitMass));
            }
            explosion.realPosition = position;
            if (scaledRigidbody.scaledTransform.inScaledSpace && explosion.TryGetComponent<AudioSource>(out var explosionAudio))
            {
                explosionAudio.enabled = false;
            }
        }
    }

    [ObserversRpc(ExcludeServer = true)]
    private void DetonateObserversRpc(Vector3d position, int hitNetId)
    {
        if (detonating)
            return;
        detonating = true;
        Vector3d hitVelocity = Vector3d.zero;
        double hitMass = 0.0;
        if (hitNetId != -1)
        {
            if (ClientManager.Objects.Spawned.TryGetValue(hitNetId, out var networkObject))
            {
                if (networkObject.TryGetComponent<ScaledRigidbody>(out var hitRB))
                {
                    hitVelocity = hitRB.velocity;
                    hitMass = hitRB.mass;
                }
                else
                {
                    Debug.LogWarning(GameLog.ComponentNotFound(this, "DetonateObserversRpc", networkObject, "ScaledRigidbody"));
                }
            }
            else
            {
                Debug.LogWarning(GameLog.NetworkObjectNotFound(this, "DetonateObserversRpc", hitNetId));
            }
        }
        InstantiateExplosion(position, hitVelocity, hitMass);
    }

    public void Detonate(ScaledRigidbody collidedRB)
    {
        if (!IsServerOrOffline)
            return;
        if (detonating) // Prevent stack overflow
            return;
        detonating = true;
        Vector3d hitVelocity = collidedRB == null ? Vector3d.zero : collidedRB.velocity;
        Vector3d explosionPoint = scaledRigidbody.scaledTransform.realPosition;
        double hitMass = collidedRB == null ? 0.0 : collidedRB.mass;
        if (!IsOffline)
        {
            int hitNetId = -1;
            if (collidedRB != null && collidedRB.TryGetComponent<NetworkObject>(out var hitNetworkObject))
            {
                hitNetId = hitNetworkObject.ObjectId;
            }
            DetonateObserversRpc(explosionPoint, hitNetId);
        }

        InstantiateExplosion(explosionPoint, hitVelocity, hitMass);

        HashSet<Transform> hitTransforms = new HashSet<Transform>();
        List<ScaledCollider> overlapColliders = ScaledSpacePhysics.Instance.GetOverlapSphere(explosionPoint, explosionRadius, ~ignoreLayers, true);
        foreach(ScaledCollider collider in overlapColliders)
        {
            ScaledRigidbody otherRB = collider.scaledRigidbody;
            if (otherRB == scaledRigidbody || hitTransforms.Contains(otherRB.transform))
                continue;
            float damage = CalculateDamage((float)(collider.GetRealCenter() - explosionPoint).sqrMagnitude);
            switch (collider.tag)
            {
                case "Ship":
                    Ship ship = otherRB.GetComponent<Ship>();

                    // Apply damage
                    if (ship.shields != null)
                    {
                        ship.shields.Damage(damage, ship.scaledRigidbody.scaledTransform.TransformRealPoint(scaledRigidbody.scaledTransform.realPosition));
                    }
                    else
                    {
                        ship.statSystem.Damage(damage);
                    }

                    // Apply force
                    otherRB.AddExplosionForce(explosionForce, explosionPoint, explosionRadius, 0, ForceMode.Impulse);
                    break;
                case "Torpedo":
                    Torpedo otherTorpedo = otherRB.GetComponent<Torpedo>();
                    if (otherTorpedo != this)
                    {
                        double distance = (otherTorpedo.scaledRigidbody.scaledTransform.realPosition - scaledRigidbody.scaledTransform.realPosition).sqrMagnitude;
                        float percent = (float)(distance / (explosionRadius * explosionRadius));
                        otherTorpedo.DelayedDetonate(collidedRB, 0.125f * percent);
                    }
                    break;
                case "Shields": // Should work fine for ship shields too
                    // Apply damage
                    otherRB.GetComponent<Shields>().Damage(damage, otherRB.scaledTransform.TransformRealPoint(explosionPoint));

                    // Apply force
                    otherRB.AddExplosionForce(explosionForce, explosionPoint, explosionRadius, 0, ForceMode.Impulse);
                    break;
                case "Projectile":
                    // Bullets and projectiles are super lightweight, so just destroy them
                    Destroy(otherRB.gameObject);
                    break;
                default:
                    // Apply damage
                    if (otherRB.TryGetComponent<StatSystem>(out var statSystem))
                        statSystem.Damage(damage);

                    // Apply force
                    otherRB.AddExplosionForce(explosionForce, explosionPoint, explosionRadius, 0, ForceMode.Impulse);
                    break;
            }

            hitTransforms.Add(otherRB.transform);
        }

        if (target != null && target.attachedRadar != null)
            target.attachedRadar.IncrementMissileLock(-1);

        if (!IsOffline)
            ServerManager.Despawn(NetworkObject);
        Destroy(gameObject);
    }

    private IEnumerator DetonateDelayRoutine(ScaledRigidbody collidedRB, float delay)
    {
        yield return new WaitForSeconds(delay);
        Detonate(collidedRB);
    }

    public void DelayedDetonate(ScaledRigidbody collidedRB, float delay)
    {
        if (!IsServerOrOffline)
            return;
        if (detonating) // Prevent stack overflow
            return;
        Debug.Log(GameLog.ObjectLog(this, "Delayed detonate."));
        StartCoroutine(DetonateDelayRoutine(collidedRB, delay));
    }

    private void OnScaledCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        Vector3d velocityA = collisionInfo.colliderA == null ? Vector3d.zero : collisionInfo.colliderA.scaledRigidbody.velocity;
        Vector3d velocityB = collisionInfo.colliderB == null ? Vector3d.zero : collisionInfo.colliderB.scaledRigidbody.velocity;
        Vector3d relativeVelocity = velocityA - velocityB;
        bool isTarget = false;
        if (target != null)
        {
            isTarget = collisionInfo.colliderB.scaledRigidbody == target.scaledRigidbody;
        }

        if (isTarget || relativeVelocity.sqrMagnitude > collideSpeedThreshold * collideSpeedThreshold)
        {
            Debug.Log(GameLog.ObjectLog(this, $"Scaled collide detonate with {collisionInfo.colliderB.name}."));
            Detonate(collisionInfo.colliderB.scaledRigidbody);
        }
    }

    private void OnScaledTrigger(ScaledCollider source, ScaledCollider other)
    {
        if (target == null || source.id != detonateTrigger.id)
            return;
        if (other.scaledRigidbody == target.scaledRigidbody || other.transform == target.transform)
        {
            Debug.Log(GameLog.ObjectLog(this, $"Scaled trigger detonate with {other.name}."));
            Detonate(other.scaledRigidbody);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        bool rbIsNull = collision.rigidbody == null;
        ScaledRigidbody otherDoubleRB = rbIsNull ? collision.transform.GetComponent<ScaledRigidbody>() : collision.rigidbody.GetComponent<ScaledRigidbody>();
        Vector3d velocityB = Vector3d.zero;
        if (otherDoubleRB == null)
        {
            if (!rbIsNull)
                velocityB = collision.rigidbody.linearVelocity.ToVector3d();
        }
        else
        {
            velocityB = otherDoubleRB.velocity;
        }
        Vector3d relativeVelocity = scaledRigidbody.velocity - velocityB;
        if (relativeVelocity.sqrMagnitude > collideSpeedThreshold * collideSpeedThreshold)
        {
            Debug.Log(GameLog.ObjectLog(this, $"Unity collide detonate with {collision.gameObject.name}."));
            Detonate(otherDoubleRB);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (target == null)
            return;
        if (scaledRigidbody.scaledTransform.inScaledSpace || other.isTrigger)
            return;

        if (other.transform == target.transform || (other.transform.TryGetComponent<RadarTarget>(out var otherTarget) && otherTarget.GetID() == target.GetID()))
        {
            Debug.Log(GameLog.ObjectLog(this, $"Unity trigger detonate with {other.name}."));
            Detonate(other.GetComponent<ScaledRigidbody>());
        }
    }
}
