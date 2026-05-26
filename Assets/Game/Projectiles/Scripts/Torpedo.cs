using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;

public class Torpedo : Projectile
{
    private bool active;

    [Header("Torpedo")] public Transform target;
    public string team;
    private Rigidbody targetRB;
    [SerializeField] private float propulsionForce = 1000f;
    [SerializeField] private float rotateSpeed = 20f;
    [SerializeField] private float moveAngleThreshold = 5f; 
    [SerializeField] private ParticleSystem rocketParticles;

    [SerializeField] private ParticleSystem explosionParticles;
    [SerializeField] private float explosionRadius = 15f;
    [SerializeField] private float explosionForce = 100f;
    [SerializeField] private float minDamage = 25f;
    [SerializeField] private float maxDamage = 75f;

    private Vector3 lastTargetVelocity;
    private Vector3 targetAcceleration;

    private Vector3 lastVelocity;
    private Vector3 acceleration;

    private Vector3 futureTargetPosition;

    protected override void Start()
    {
        base.Start();
        doubleRigidbody.OnScaledCollisionEnter += (_, x) => Detonate(x.contactPoint.ToVector3());
    }

    private void FixedUpdate()
    {
        if (active)
        {
            if (target != null)
            {
                /*Vector3 targetVelocity = Vector3.zero;
                if(targetRB != null)
                    targetVelocity = targetRB.velocity;
                targetAcceleration = (targetVelocity - lastTargetVelocity) / Time.fixedDeltaTime;
                acceleration = (attachedRB.velocity - lastVelocity) / Time.fixedDeltaTime;
                Vector3 relativePosition = target.position - transform.position;
                Vector3 relativeVelocity = targetVelocity - attachedRB.velocity;
                Vector3 relativeAcceleration = targetAcceleration - acceleration;

                float speed = attachedRB.velocity.magnitude;
                float predictionTime = speed > 25f ? relativePosition.magnitude / speed : 5f;

                futureTargetPosition = target.position + (relativeVelocity * predictionTime) + (0.5f * predictionTime * predictionTime * relativeAcceleration);

                Vector3 targetDirection = (futureTargetPosition - transform.position).normalized;*/
                Vector3 targetDirection = (target.position - transform.position).normalized;

                //Debug.DrawLine(transform.position, futureTargetPosition, Color.green, 0.1f);
                Debug.DrawLine(transform.position, transform.position + targetDirection * 100, Color.green, 0.1f);

                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(targetDirection), Time.fixedDeltaTime * rotateSpeed);

                if (Vector3.Angle(transform.forward, targetDirection) < moveAngleThreshold)
                {
                    doubleRigidbody.AddForce((transform.forward * propulsionForce).ToVector3d(), ForceMode.Force);
                    if (!rocketParticles.isPlaying)
                        rocketParticles.Play();
                }
                else
                {
                    if (rocketParticles.isPlaying)
                    {
                        rocketParticles.Stop();
                        rocketParticles.Clear();
                    }
                }

                //lastTargetVelocity = targetVelocity;
                lastVelocity = doubleRigidbody.velocity.ToVector3();
            }
            else
            {
                doubleRigidbody.AddForce((transform.forward * propulsionForce).ToVector3d(), ForceMode.Force);
            }
        }
        else
        {
            if (rocketParticles.isPlaying)
            {
                rocketParticles.Stop();
                rocketParticles.Clear();
            }
        }
    }

    public void Activate(Transform target, float delay)
    {
        this.target = target;
        target.TryGetComponent(out targetRB);
        StartCoroutine(ActivateRoutine(delay));
    }

    private IEnumerator ActivateRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        rocketParticles.Play();
        active = true;
        GetComponent<Collider>().enabled = true;
    }

    private float CalculateDamage(float sqrDistance)
    {
        // y = -(maxHeight / intercept^2) * x^2 + maxHeight + minHeight;
        return -(maxDamage / (explosionRadius * explosionRadius)) * sqrDistance + maxDamage + minDamage;
    }

    public void Detonate(Vector3 contactPoint)
    {
        Instantiate(explosionParticles, transform.position, transform.rotation);
        Collider[] colliders = new Collider[64];
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
                        hitRigidbody.GetComponent<Torpedo>().Detonate(contactPoint);
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

    private void OnCollisionEnter(Collision collision)
    {
        Detonate(collision.GetContact(0).point);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (doubleRigidbody.scaledTransform.inScaledSpace)
            return;
        if (!other.isTrigger)
        {
            // Within range of target, detonate
            if (other.transform == target || (targetRB != null && other.attachedRigidbody == targetRB))
            {
                Detonate(transform.position);
            }
        }
    }
}
