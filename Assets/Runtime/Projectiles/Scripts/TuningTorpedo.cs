using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(ScaledRigidbody))]
public class TuningTorpedo : MonoBehaviour
{
    public ScaledRigidbody scaledRigidbody;
    private bool active;
    private bool canThrust;

    [Header("Torpedo")]
    public bool killOnDetonate;
    [SerializeField] private CapsuleCollider _collider;
    [SerializeField] private ScaledCollider[] scaledColliders;
    [SerializeField] private ScaledCollider detonateTrigger;
    [SerializeField] private float engineForce = 10000f;
    [SerializeField] private float thrusterForce = 100f;

    public float proportionalGain = 16.0f;
    public float integralGain = 0.0f;
    public float derivativeGain = 4.0f;

    private PIDController xPID;
    private PIDController yPID;
    private PIDController zPID;

    [SerializeField] private GameObject rocketTrail;

    [SerializeField, Tooltip("Collisions with a relative speed above this will detonate the torpedo.")] private float collideSpeedThreshold = 100f;
    [SerializeField] private GameObject explosionPrefab;
    
    [SerializeField] private RadarTarget target;
    public float navigationConstant = 4f;
    [SerializeField] private float thrusterTorque;
    private float engineAcceleration;

    public Action<TuningTorpedo> OnDetonated;

    [SerializeField] private float alignmentWeight = 1f;
    [SerializeField] private float angularSpeedWeight = 0.1f;
    [SerializeField] private float timeWeight = 1f;
    [SerializeField] private float detonateReward = 100f;
    public float fitness;
    private float startTime = -1f;
    private int numDetonations = 0;

    public Vector3 desiredForward;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        thrusterTorque = _collider.height * thrusterForce;
        engineAcceleration = (float)(engineForce / scaledRigidbody.mass);
        if (!killOnDetonate)
        {
            StartCoroutine(RemoveCollider(scaledRigidbody.attachedRigidbody.inertiaTensor, scaledRigidbody.attachedRigidbody.inertiaTensorRotation, scaledRigidbody.attachedRigidbody.centerOfMass));
            Destroy(_collider);
            foreach(ScaledCollider scaledCollider in scaledColliders)
            {
                Destroy(scaledCollider);
            }
        }
        else
        {
            scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
            scaledRigidbody.OnScaledTriggerEnter += OnScaledTrigger;
        }

        xPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        yPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        zPID = new PIDController(proportionalGain, integralGain, derivativeGain);
    }
    
    private IEnumerator RemoveCollider(Vector3 tensor, Quaternion tensorRot, Vector3 com)
    {
        yield return new WaitForFixedUpdate();
        scaledRigidbody.attachedRigidbody.inertiaTensor = tensor;
        scaledRigidbody.attachedRigidbody.inertiaTensorRotation = tensorRot;
        scaledRigidbody.attachedRigidbody.centerOfMass = com;
    }

    private void OnDestroy()
    {
        scaledRigidbody.OnScaledCollisionEnter -= OnScaledCollide;
        scaledRigidbody.OnScaledTriggerEnter -= OnScaledTrigger;
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
            // Debug.Log(GameLog.ObjectLog(this, $"Applying local force: {localForce} vs desired: {localDesiredForce}"));

            // Z axis stays the same — per-axis clamping is correct here
            if (localForce.z >= 0.0f)
            {
                SetRocketTrailActive(true);
                float trailScale = localForce.z / engineForce;
                rocketTrail.transform.localScale = Vector3.one * trailScale;
                fitness += trailScale * alignmentWeight;
            }
            else
            {
                SetRocketTrailActive(false);
            }
            fitness -= (float)scaledRigidbody.angularVelocity.sqrMagnitude * angularSpeedWeight;
        }
        else
        {
            SetRocketTrailActive(false);
        }

        // Rotate torpedo to align with desired force
        if (desiredForce.sqrMagnitude > 0.0001)
        {
            desiredForward = desiredForce.normalized.ToVector3();

            Debug.DrawRay(transform.position, desiredForce.ToVector3(), Color.green);
            
            Vector3 worldRotationError = Vector3.Cross(transform.forward, desiredForward);

            Vector3 torque = new Vector3(
                xPID.GetOutput(worldRotationError.x, scaledRigidbody.angularVelocity.x, Time.fixedDeltaTime),
                yPID.GetOutput(worldRotationError.y, scaledRigidbody.angularVelocity.y, Time.fixedDeltaTime),
                zPID.GetOutput(worldRotationError.z, scaledRigidbody.angularVelocity.z, Time.fixedDeltaTime)
            );

            torque = Vector3.ClampMagnitude(torque, thrusterTorque);

            scaledRigidbody.AddTorque(torque, ForceMode.Force);

            // // Convert the angular error into the torpedo's LOCAL space.
            // // localRotationError.x = rotation around local X axis
            // // localRotationError.y = rotation around local Y axis
            // // localRotationError.z = rotation around local Z axis
            // Vector3 localRotationError = transform.InverseTransformDirection(worldRotationError);

            // // PID controllers now operate on local rotational axes.
            // Vector3 localTorque = new Vector3(
            //     xPID.GetOutput(localRotationError.x, Time.fixedDeltaTime),
            //     yPID.GetOutput(localRotationError.y, Time.fixedDeltaTime),
            //     0f
            // );

            // // Convert the PID output into torque.
            // localTorque *= thrusterTorque;

            // // Limit the total available torque.
            // localTorque = Vector3.ClampMagnitude(localTorque, thrusterTorque);

            // // Apply torque in LOCAL SPACE.
            // scaledRigidbody.AddRelativeTorque(localTorque.ToVector3d(), ForceMode.Force);
        }
    }

    private void SetRocketTrailActive(bool active)
    {
        if(rocketTrail.activeSelf != active)
        {
            rocketTrail.SetActive(active);
        }
    }

    public void Activate(RadarTarget target, float delay)
    {
        fitness = 0f;
        startTime = Time.time;
        numDetonations = 0;

        this.target = target;
        StartCoroutine(ActivateRoutine(delay));
    }

    private IEnumerator ActivateRoutine(float delay)
    {
        active = true;
        yield return new WaitForSeconds(delay);
        canThrust = true;
        if (killOnDetonate)
        {
            _collider.enabled = true;
            scaledRigidbody.EnableScaledColliders(true);
        }
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

    public void Detonate(ScaledRigidbody collidedRB)
    {
        if (!active)
            return;
        numDetonations++;
        fitness += detonateReward * numDetonations - (Time.time - startTime) * timeWeight;
        OnDetonated?.Invoke(this);
        Vector3d hitVelocity = collidedRB == null ? Vector3d.zero : collidedRB.velocity;
        Vector3d explosionPoint = scaledRigidbody.scaledTransform.realPosition;
        double hitMass = collidedRB == null ? 0.0 : collidedRB.mass;

        InstantiateExplosion(explosionPoint, hitVelocity, hitMass);

        if (killOnDetonate)
        {
            active = false;
            canThrust = false;
            gameObject.SetActive(false);
        }
    }

    private void OnScaledCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        Vector3d velocityA = collisionInfo.colliderA == null ? Vector3d.zero : collisionInfo.colliderA.scaledRigidbody.velocity;
        Vector3d velocityB = collisionInfo.colliderB == null ? Vector3d.zero : collisionInfo.colliderB.scaledRigidbody.velocity;
        Vector3d relativeVelocity = velocityA - velocityB;
        bool isTarget = target != null && collisionInfo.colliderB.scaledRigidbody == target.scaledRigidbody;

        if (isTarget || relativeVelocity.sqrMagnitude > collideSpeedThreshold * collideSpeedThreshold)
        {
            // Debug.Log(GameLog.ObjectLog(this, $"Scaled collide detonate with {collisionInfo.colliderB.name}."));
            Detonate(collisionInfo.colliderB.scaledRigidbody);
        }
    }

    private void OnScaledTrigger(ScaledCollider source, ScaledCollider other)
    {
        if (target == null || source.id != detonateTrigger.id)
            return;
        if (other.scaledRigidbody == target.scaledRigidbody || other.transform == target.transform)
        {
            // Debug.Log(GameLog.ObjectLog(this, $"Scaled trigger detonate with {other.name}."));
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
            // Debug.Log(GameLog.ObjectLog(this, $"Unity collide detonate with {collision.gameObject.name}."));
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
            // Debug.Log(GameLog.ObjectLog(this, $"Unity trigger detonate with {other.name}."));
            Detonate(other.GetComponent<ScaledRigidbody>());
        }
    }

    public void SetPIDGains(float proportional, float integral, float derivative)
    {
        proportionalGain = Mathf.Max(0f, proportional);
        integralGain = Mathf.Max(0f, integral);
        derivativeGain = Mathf.Max(0f, derivative);

        xPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        yPID = new PIDController(proportionalGain, integralGain, derivativeGain);
        zPID = new PIDController(proportionalGain, integralGain, derivativeGain);
    }
}
