using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;
using FishNet.Object;
using FishNet;

[RequireComponent(typeof(ScaledRigidbody), typeof(RadarTarget))]
public class Torpedo : NetworkBehaviour
{
    private ScaledRigidbody scaledRigidbody;
    private RadarTarget thisRadarTarget;
    private PIDController xPID;
    private PIDController yPID;
    private PIDController zPID;

    [Header("Torpedo")]
    [SerializeField] private bool active;
    [SerializeField] private CapsuleCollider _collider;
    [SerializeField] private float engineForce = 10000f;
    [SerializeField] private float thrusterForce = 100f;

    [SerializeField] private float proportionalGain = 16.0f;
    [SerializeField] private float integralGain = 0.0f;
    [SerializeField] private float derivativeGain = 4.0f;

    [SerializeField] private GameObject rocketParticles;

    [SerializeField, Tooltip("Collisions with a relative speed above this will detonate the torpedo.")] private float collideSpeedThreshold = 100f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionRadius = 15f;
    [SerializeField] private float explosionForce = 100f;
    [SerializeField] private float minDamage = 25f;
    [SerializeField] private float maxDamage = 75f;
    
    [SerializeField] private LayerMask ignoreLayers;
    [SerializeField] private RadarTarget target;
    [SerializeField] private float navigationConstant = 4f;
    private float thrusterTorque;
    private float engineAcceleration;

    // Need to use this to prevent stack overflows
    private bool detonating = false;

    private bool IsServerOrOffline => IsServerInitialized || IsOffline;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        thisRadarTarget = GetComponent<RadarTarget>();
        thrusterTorque = _collider.height * thrusterForce;
        engineAcceleration = engineForce / scaledRigidbody.attachedRigidbody.mass;
        xPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        yPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        zPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        if (IsOffline)
        {
            scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
            scaledRigidbody.OnScaledTriggerEnter += OnScaledTrigger;
        }
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
        if (!IsServerOrOffline)
            return;
        if (!active || target == null)
        {
            if (rocketParticles.activeSelf)
                rocketParticles.SetActive(false);
            return;
        }

        Vector3d targetVelocity = target.scaledRigidbody.velocity;
        Vector3d torpedoVelocity = thisRadarTarget.scaledRigidbody.velocity;
        Vector3d realTargetPosition = target.scaledRigidbody.scaledTransform.realPosition;
        Vector3d realTorpedoPosition = thisRadarTarget.scaledRigidbody.scaledTransform.realPosition;

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
        Vector3d torpedoDir = torpedoVelocity.normalized;
        double perpendicularFactor = 1.0 - Math.Abs(Vector3d.Dot(torpedoDir, targetDir));

        // Blend a braking force into the acceleration, scaled by how sideways we are
        Vector3d brakeAcceleration = -torpedoDir * engineAcceleration * perpendicularFactor;
        Vector3d desiredAcceleration = targetDir * engineAcceleration + pnAcceleration + brakeAcceleration;

        Vector3d desiredForce = desiredAcceleration * scaledRigidbody.attachedRigidbody.mass;
        Vector3 localDesiredForce = transform.InverseTransformDirection(desiredForce.ToVector3());
        
        // Clamp lateral (X/Y) force by magnitude, not per-component
        // This preserves steering direction when saturated
        Vector2 lateralForce = new Vector2(localDesiredForce.x, localDesiredForce.y);
        if (lateralForce.sqrMagnitude > thrusterForce * thrusterForce)
            lateralForce = lateralForce.normalized * thrusterForce;

        localDesiredForce.x = lateralForce.x;
        localDesiredForce.y = lateralForce.y;

        // Z axis stays the same — per-axis clamping is correct here
        if (localDesiredForce.z >= 0.0f)
        {
            localDesiredForce.z = Mathf.Min(localDesiredForce.z, engineForce);
            if (!rocketParticles.activeSelf)
                rocketParticles.SetActive(true);
        }
        else
        {
            localDesiredForce.z = Mathf.Max(localDesiredForce.z, -thrusterForce);
            if (rocketParticles.activeSelf)
                rocketParticles.SetActive(false);
        }

        scaledRigidbody.AddRelativeForce(localDesiredForce.ToVector3d(), ForceMode.Force);

        // Rotate engine to align with desired force
        // Desired facing direction
        Vector3 desiredForward = desiredForce.normalized.ToVector3();
        Debug.DrawRay(transform.position, desiredForce.ToVector3(), Color.green);

        if (desiredForward.sqrMagnitude > 0.0001f)
        {
            Vector3 rotationError = Vector3.Cross(transform.forward, desiredForward);

            Vector3 torque = new Vector3(
                xPID.GetOutput(rotationError.x, Time.fixedDeltaTime) * thrusterTorque,
                yPID.GetOutput(rotationError.y, Time.fixedDeltaTime) * thrusterTorque,
                zPID.GetOutput(rotationError.z, Time.fixedDeltaTime) * thrusterTorque
            );
            torque = Vector3.ClampMagnitude(torque, thrusterTorque);

            scaledRigidbody.AddTorque(torque.ToVector3d(), ForceMode.Force);
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
        yield return new WaitForSeconds(delay);
        active = true;
        _collider.enabled = true;
        scaledRigidbody.EnableScaledColliders(true);
        if (thisRadarTarget.alertWhenTargeting && target != null && target.alertSystem != null)
            target.alertSystem.IncrementTorpedoLock(1);
    }

    public void SetTarget(RadarTarget newTarget)
    {
        if (!IsServerOrOffline)
            return;
        if (thisRadarTarget.alertWhenTargeting && target != null && target.alertSystem != null)
            target.alertSystem.IncrementTorpedoLock(-1);
        target = newTarget;
        if (thisRadarTarget.alertWhenTargeting && target != null && target.alertSystem != null)
            target.alertSystem.IncrementTorpedoLock(1);

        if (target == thisRadarTarget)
        {
            Detonate(scaledRigidbody.scaledTransform.realPosition);
        }
    }

    private float CalculateDamage(float sqrDistance)
    {
        // y = -(maxHeight / intercept^2) * x^2 + maxHeight + minHeight;
        return Mathf.Max(-(maxDamage / (explosionRadius * explosionRadius)) * sqrDistance + maxDamage + minDamage, 0f);
    }

    [ObserversRpc(ExcludeServer = true)]
    private void DetonateObserversRpc(double x, double y, double z)
    {
        if (detonating)
            return;
        detonating = true;

        if (scaledRigidbody.scaledTransform.visible)
        {
            ScaledTransform explosion = Instantiate(explosionPrefab, transform.position, transform.rotation).GetComponent<ScaledTransform>();
            explosion.realPosition = new Vector3d(x, y, z);
        }
    }

    public void Detonate(Vector3d contactPoint)
    {
        if (!IsServerOrOffline)
            return;
        if (detonating) // Prevent stack overflow
            return;
        detonating = true;
        if (!IsOffline)
            DetonateObserversRpc(contactPoint.x, contactPoint.y, contactPoint.z);

        Vector3 contactOriginLocalPoint = (contactPoint - FloatingWorldOrigin.Instance.scaledTransform.realPosition).ToVector3();

        ScaledTransform explosion = Instantiate(explosionPrefab, transform.position, transform.rotation).GetComponent<ScaledTransform>();
        explosion.realPosition = contactPoint;

        HashSet<Transform> hitTransforms = new HashSet<Transform>();
        List<ScaledCollider> overlapColliders = ScaledSpacePhysics.Instance.GetOverlapSphere(contactPoint, explosionRadius, ~ignoreLayers, true);
        foreach(ScaledCollider collider in overlapColliders)
        {
            ScaledRigidbody otherRB = collider.scaledRigidbody;
            if (otherRB == scaledRigidbody || hitTransforms.Contains(otherRB.transform))
                continue;
            float damage = CalculateDamage((float)(collider.GetRealCenter() - contactPoint).sqrMagnitude);
            switch (collider.tag)
            {
                case "Ship":
                    Ship ship = otherRB.GetComponent<Ship>();

                    // Apply damage
                    if (ship.shields != null)
                    {
                        ship.shields.Damage(damage, contactOriginLocalPoint);
                    }
                    else
                    {
                        ship.statSystem.Damage(damage);
                    }

                    // Apply force
                    otherRB.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0, ForceMode.Impulse);
                    break;
                case "Torpedo":
                    Torpedo otherTorpedo = otherRB.GetComponent<Torpedo>();
                    if (otherTorpedo != this)
                        otherTorpedo.Detonate(contactPoint);
                    break;
                case "Shields": // Should work fine for ship shields too
                    // Apply damage
                    otherRB.GetComponent<Shields>().Damage(damage, contactOriginLocalPoint);

                    // Apply force
                    otherRB.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0, ForceMode.Impulse);
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
                    otherRB.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0, ForceMode.Impulse);
                    break;
            }

            hitTransforms.Add(otherRB.transform);
        }

        if (target != null && target.alertSystem != null)
            target.alertSystem.IncrementTorpedoLock(-1);

        if (!IsOffline)
            InstanceFinder.ServerManager.Despawn(NetworkObject);
        Destroy(gameObject);
    }

    private void OnScaledCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        Vector3d velocityA = collisionInfo.colliderA == null ? Vector3d.zero : collisionInfo.colliderA.scaledRigidbody.velocity;
        Vector3d velocityB = collisionInfo.colliderB == null ? Vector3d.zero : collisionInfo.colliderB.scaledRigidbody.velocity;
        Vector3d relativeVelocity = velocityA - velocityB;
        if (relativeVelocity.sqrMagnitude > collideSpeedThreshold * collideSpeedThreshold)
            Detonate(collisionInfo.contactPoint);
    }

    private void OnScaledTrigger(ScaledCollider otherCollider)
    {
        if (target == null)
            return;
        if (otherCollider.scaledRigidbody == target.scaledRigidbody || otherCollider.transform == target.transform)
        {
            Detonate(scaledRigidbody.scaledTransform.realPosition);
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
        Vector3d contactPoint = scaledRigidbody.scaledTransform.TransformRenderPoint(collision.GetContact(0).point);
        if (relativeVelocity.sqrMagnitude > collideSpeedThreshold * collideSpeedThreshold)
            Detonate(contactPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (target == null)
            return;
        if (scaledRigidbody.scaledTransform.inScaledSpace || other.isTrigger)
            return;

        if (other == target.transform || (other.transform.TryGetComponent<RadarTarget>(out var otherTarget) && otherTarget.GetID() == target.GetID()))
        {
            Detonate(scaledRigidbody.scaledTransform.realPosition);
        }
    }
}
