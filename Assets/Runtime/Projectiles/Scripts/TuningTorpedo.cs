using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(RadarTarget))]
public class TuningTorpedo : Projectile
{
    private RadarTarget thisRadarTarget;
    private PIDController xPID;
    private PIDController yPID;
    private PIDController zPID;

    [SerializeField] private bool active;
    [Header("Torpedo"), SerializeField] private CapsuleCollider _collider;
    [SerializeField] private float engineForce = 10000f;
    [SerializeField] private float thrusterForce = 100f;

    public float lateralDampGain = 1.0f;
    public float proportionalGain = 16.0f;
    public float integralGain = 0.0f;
    public float derivativeGain = 4.0f;

    [SerializeField] private GameObject rocketParticles;

    [SerializeField, Tooltip("Collisions with a relative speed above this will detonate the torpedo.")] private float collideSpeedThreshold = 100f;
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private float explosionRadius = 15f;
    [SerializeField] private float explosionForce = 100f;
    [SerializeField] private float minDamage = 25f;
    [SerializeField] private float maxDamage = 75f;

    [SerializeField] private RadarTarget target;
    [SerializeField] private float navigationConstant = 4f;
    private float thrusterTorque;
    private float engineAcceleration;

    // Need to use this to prevent stack overflows
    private bool detonating = false;

    public Vector3 desiredForward;
    public Action<int> OnDetonate;
    public int index;

    private void Start()
    {
        thisRadarTarget = GetComponent<RadarTarget>();
        thrusterTorque = _collider.height * thrusterForce;
        engineAcceleration = engineForce / doubleRigidbody.attachedRigidbody.mass;
        xPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        yPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        zPID = new PIDController(proportionalGain, integralGain, derivativeGain);
    }

    private void FixedUpdate()
    {
        if (!active || target == null)
        {
            if (rocketParticles.activeSelf)
                rocketParticles.SetActive(false);
            return;
        }

        Vector3d targetVelocity = target.doubleRigidbody.velocity;
        Vector3d torpedoVelocity = thisRadarTarget.doubleRigidbody.velocity;
        Vector3d realTargetPosition = target.doubleRigidbody.scaledTransform.realPosition;
        Vector3d realTorpedoPosition = thisRadarTarget.doubleRigidbody.scaledTransform.realPosition;

        Vector3d relativePosition = realTargetPosition - realTorpedoPosition;
        Vector3d relativeVelocity = targetVelocity - torpedoVelocity;
        double sqrDistance = relativePosition.sqrMagnitude;
        Vector3d targetDir = relativePosition / Math.Sqrt(sqrDistance);

        // Proportional Navigation Guidance to steer torpedo: a = cross(NV_r, omega)
        // omega = cross(R, V_r) / dot(R, R)
        // R = relative position
        Vector3d rotationVector = Vector3d.Cross(relativePosition, relativeVelocity) / sqrDistance;
        Vector3d pnAcceleration = Vector3d.Cross(navigationConstant * relativeVelocity, rotationVector);

        // Project relative velocity onto LOS; remainder is the "orbit" component
        Vector3d closingComponent = Vector3d.Dot(relativeVelocity, targetDir) * targetDir;
        Vector3d lateralRelVel    = relativeVelocity - closingComponent;

        // Suppress lateral relative velocity — tune lateralDampGain (0.3–1.0)
        Vector3d antiOrbit = -lateralRelVel * lateralDampGain;
        Vector3d desiredAcceleration = targetDir * engineAcceleration + pnAcceleration + antiOrbit;

        Vector3d desiredForce = desiredAcceleration * doubleRigidbody.attachedRigidbody.mass;
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

        doubleRigidbody.AddRelativeForce(localDesiredForce.ToVector3d(), ForceMode.Force);

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

            doubleRigidbody.AddTorque(torque.ToVector3d(), ForceMode.Force);
        }
    }

    public void Activate(RadarTarget target, float delay)
    {
        this.target = target;
        StartCoroutine(ActivateRoutine(delay));
    }

    private IEnumerator ActivateRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        active = true;
        _collider.enabled = true;
        if (target != null && target.alertSystem != null)
            target.alertSystem.AddTorpedoLock();
    }

    public void SetTarget(RadarTarget newTarget)
    {
        target = newTarget;
        
        if (target == thisRadarTarget)
        {
            Detonate(doubleRigidbody.scaledTransform.realPosition);
        }
    }

    public void Detonate(Vector3d contactPoint)
    {
        if (detonating) // Prevent stack overflow
            return;
        OnDetonate?.Invoke(index);
        detonating = true;

        if (!doubleRigidbody.scaledTransform.inScaledSpace)
        {
            Instantiate(explosionPrefab, transform.position, transform.rotation);
        }

        Destroy(gameObject);
    }

    protected override void OnCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        if (collisionInfo.colliderB.doubleRigidbody.CompareTag("Torpedo"))
            return;
        Vector3d relativeVelocity = collisionInfo.colliderA.doubleRigidbody.velocity - collisionInfo.colliderB.doubleRigidbody.velocity;
        if (relativeVelocity.sqrMagnitude > collideSpeedThreshold * collideSpeedThreshold)
            Detonate(collisionInfo.contactPoint);
    }

    protected override void OnTrigger(Transform other, ScaledCollider otherDoubleRB)
    {
        if (target == null)
            return;
        if (other == target.transform || otherDoubleRB.doubleRigidbody == target.doubleRigidbody)
        {
            Detonate(doubleRigidbody.scaledTransform.realPosition);
        }
    }
}
