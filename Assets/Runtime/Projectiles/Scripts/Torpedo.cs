using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;

[RequireComponent(typeof(RadarTarget))]
public class Torpedo : Projectile
{
    [SerializeField] private bool active;
    private RadarTarget thisRadarTarget;
    [Header("Torpedo"), SerializeField] private CapsuleCollider _collider;
    [SerializeField] private float engineForce = 10000f;
    [SerializeField] private float thrusterForce = 100f;
    [SerializeField] private float damping = 2.0f;
    [SerializeField] private GameObject rocketParticles;

    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private float explosionRadius = 15f;
    [SerializeField] private float explosionForce = 100f;
    [SerializeField] private float minDamage = 25f;
    [SerializeField] private float maxDamage = 75f;

    [SerializeField] private RadarTarget target;
    private float thrusterTorque;
    private float engineAcceleration;

    // Need to use this to prevent stack overflows
    private bool detonating = false;

    private void Start()
    {
        thisRadarTarget = GetComponent<RadarTarget>();
        thrusterTorque = _collider.height * thrusterForce;
        engineAcceleration = engineForce / doubleRigidbody.attachedRigidbody.mass;
    }

    private void FixedUpdate()
    {
        if (active)
        {
            if (target != null)
            {
                // Predict target's future position based on its acceleration and velocity
                Vector3d targetVelocity = target.doubleRigidbody.velocity;
                Vector3d torpedoVelocity = thisRadarTarget.doubleRigidbody.velocity;
                Vector3d realTargetPosition = target.doubleRigidbody.scaledTransform.realPosition;
                Vector3d realTorpedoPosition = thisRadarTarget.doubleRigidbody.scaledTransform.realPosition;

                Vector3d relativePosition = realTargetPosition - realTorpedoPosition;
                double distance = relativePosition.magnitude;
                Vector3d direction = relativePosition / distance;
                Vector3d relativeAcceleration = target.acceleration - thisRadarTarget.acceleration;
                Vector3d relativeVelocity = targetVelocity - torpedoVelocity;

                double closingAcceleration = -Vector3d.Dot(relativeAcceleration, direction);
                double closingSpeed = -Vector3d.Dot(relativeVelocity, direction);
                double arrivalTime = SpaceMath.CalculateArrivalTime(closingAcceleration - engineAcceleration, closingSpeed, distance);

                if (arrivalTime < 0.0) // Will never arrive
                    return;

                Vector3d predictedRelativePosition = relativePosition + (relativeVelocity * arrivalTime) + (0.5 * arrivalTime * arrivalTime * relativeAcceleration);

                // Apply force to get to desired future position
                Vector3 desiredLocalVelocity = transform.InverseTransformDirection((predictedRelativePosition * 1000f).ToVector3());
                Vector3 localVelocity = transform.InverseTransformDirection(torpedoVelocity.ToVector3());
                Vector3 error = desiredLocalVelocity - localVelocity;
                
                error = Vector3.ClampMagnitude(error, 1.0f);
                error.x *= thrusterForce;
                error.y *= thrusterForce;
                error.z = error.z > 0 ? error.z * engineForce : error.z * thrusterForce;

                doubleRigidbody.AddRelativeForce(error.ToVector3d(), ForceMode.Force);

                Debug.DrawLine(transform.position, transform.position + predictedRelativePosition.ToVector3(), Color.green, Time.fixedDeltaTime);
                // Rotate to align engines with error
                // Desired facing direction
                Vector3 desiredForward = predictedRelativePosition.normalized.ToVector3();

                // Current angular velocity in world space
                Vector3 angularVelocity = doubleRigidbody.angularVelocity.ToVector3();

                // Rotation error quaternion
                Quaternion rotationError = Quaternion.FromToRotation(transform.forward, desiredForward);

                // Convert to axis-angle
                rotationError.ToAngleAxis(out float angleDeg, out Vector3 axis);

                if (float.IsNaN(axis.x))
                    return;

                // Convert to signed shortest-angle
                if (angleDeg > 180f)
                    angleDeg -= 360f;

                // Very small errors cause jitter
                if (Mathf.Abs(angleDeg) < 0.1f)
                    return;

                // Convert degrees to radians
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // PD controller
                Vector3 torque = axis.normalized * (angleRad * thrusterTorque) - angularVelocity * damping;

                // Clamp maximum torque
                torque = Vector3.ClampMagnitude(torque, thrusterTorque);

                doubleRigidbody.AddTorque(torque.ToVector3d(), ForceMode.Force);
            }
        }
        else
        {
            if (rocketParticles.activeSelf)
            {
                rocketParticles.SetActive(false);
            }
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
        rocketParticles.SetActive(true);
        active = true;
        _collider.enabled = true;
    }

    private float CalculateDamage(float sqrDistance)
    {
        // y = -(maxHeight / intercept^2) * x^2 + maxHeight + minHeight;
        return -(maxDamage / (explosionRadius * explosionRadius)) * sqrDistance + maxDamage + minDamage;
    }

    public void Detonate(Vector3 contactPoint, Torpedo caller)
    {
        if (detonating)
            return;
        detonating = true;

        if (doubleRigidbody.scaledTransform.inScaledSpace)
        {
            Destroy(gameObject);
            return;
        }

        Instantiate(explosionParticles, transform.position, transform.rotation);
        Collider[] colliders = new Collider[128];
        HashSet<Transform> hitTransforms = new HashSet<Transform>();
        int hits = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, colliders, ~ignoreLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits; i++)
        {
            Rigidbody hitRigidbody = colliders[i].attachedRigidbody;
            if (hitRigidbody != doubleRigidbody.attachedRigidbody && !hitTransforms.Contains(hitRigidbody.transform))
            {
                float damage = CalculateDamage((transform.position - hitRigidbody.transform.position).sqrMagnitude);

                switch (hitRigidbody.tag)
                {
                    case "Ship":
                        Ship ship = hitRigidbody.GetComponent<Ship>();

                        // Apply damage
                        if (ship.shields != null)
                        {
                            ship.shields.Damage(damage, contactPoint);
                        }
                        else
                        {
                            ship.statSystem.Damage(damage);
                        }

                        // Apply force
                        ship.doubleRigidbody.AddExplosionForce(explosionForce, contactPoint.ToVector3d(), explosionRadius, 0, ForceMode.Impulse);
                        break;
                    case "Torpedo":
                        Torpedo otherTorpedo = hitRigidbody.GetComponent<Torpedo>();
                        if (otherTorpedo != this && otherTorpedo != caller) // Prevent stack overflow
                            otherTorpedo.Detonate(contactPoint, this);
                        break;
                    case "Shields": // Should work fine for ship shields too
                        // Apply damage
                        hitRigidbody.GetComponent<Shields>().Damage(damage, contactPoint);

                        // Apply force
                        if (hitRigidbody.TryGetComponent<DoubleRigidbody>(out var shieldDoubleRigidbody))
                        {
                            shieldDoubleRigidbody.AddExplosionForce(explosionForce, contactPoint.ToVector3d(), explosionRadius, 0, ForceMode.Impulse);
                        }
                        else
                        {
                            hitRigidbody.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0, ForceMode.Impulse);
                        }
                        break;
                    case "Turret":

                        break;
                    case "Projectile":
                        // Bullets and projectiles are super lightweight, so just destroy them
                        Destroy(hitRigidbody.gameObject);
                        break;
                    default:
                        // Apply damage
                        if (hitRigidbody.TryGetComponent<StatSystem>(out var statSystem))
                            statSystem.Damage(damage);

                        // Apply force
                        if (hitRigidbody.TryGetComponent<DoubleRigidbody>(out var doubleRigidbody))
                        {
                            doubleRigidbody.AddExplosionForce(explosionForce, contactPoint.ToVector3d(), explosionRadius, 0, ForceMode.Impulse);
                        }
                        else
                        {
                            hitRigidbody.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0, ForceMode.Impulse);
                        }
                        break;
                }

                hitTransforms.Add(hitRigidbody.transform);
            }
        }

        Destroy(gameObject);
    }

    protected override void OnCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        Detonate(collisionInfo.contactPoint.ToVector3(), this);
    }

    protected override void OnTrigger(Transform other, DoubleRigidbody otherDoubleRB)
    {
        if (target == null)
            return;
        if (other == target.transform || otherDoubleRB == target.doubleRigidbody)
        {
            Detonate(transform.position, this);
        }
    }
}
