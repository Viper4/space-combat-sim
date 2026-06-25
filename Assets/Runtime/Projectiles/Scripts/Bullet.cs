using System.Collections;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

/// <summary>
/// No need for NetworkBehaviour since we only sychronize the instantiation and let each client visually simulate the bullet.
/// Actual physics and effects of the bullet is handled by server only to determine hits/detonations/etc.
/// </summary>
[RequireComponent(typeof(ScaledRigidbody))]
public class Bullet : MonoBehaviour
{
    private ScaledRigidbody scaledRigidbody;

    [SerializeField, Tooltip("Additional damage to apply besides already calculated collision damage.")] private float damageAmount = 10;
    [SerializeField] private float destroyDelay = 5;

    private void Awake()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        if(destroyDelay > 0)
        {
            Destroy(gameObject, destroyDelay);
        }
        scaledRigidbody.OnScaledCollisionEnter += OnScaledCollide;
    }

    private void OnDestroy()
    {
        scaledRigidbody.OnScaledCollisionEnter -= OnScaledCollide;
    }

    private void OnScaledCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
    {
        switch(collisionInfo.transformB.tag)
        {
            case "Ship":
                Ship ship = collisionInfo.transformB.GetComponent<Ship>();
                ship.statSystem.Damage(damageAmount);
                break;
            case "Torpedo":
                collisionInfo.transformB.GetComponent<Torpedo>().Detonate(collisionInfo.contactPoint);
                break;
            case "Shields":
                collisionInfo.transformB.GetComponent<Shields>().Damage(damageAmount, collisionInfo.contactPoint.ToVector3());
                break;
            default:
                if (collisionInfo.transformB.TryGetComponent<StatSystem>(out var statSystem))
                {
                    statSystem.Damage(damageAmount);
                }
                break;
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 contactPoint = collision.GetContact(0).point;
        switch(collision.transform.tag)
        {
            case "Ship":
                Ship ship = collision.transform.GetComponent<Ship>();
                ship.statSystem.Damage(damageAmount);
                break;
            case "Torpedo":
                collision.transform.GetComponent<Torpedo>().Detonate(scaledRigidbody.scaledTransform.TransformRenderPoint(contactPoint));
                break;
            case "Shields":
                collision.transform.GetComponent<Shields>().Damage(damageAmount, contactPoint);
                break;
            default:
                if (collision.transform.TryGetComponent<StatSystem>(out var statSystem))
                {
                    statSystem.Damage(damageAmount);
                }
                break;
        }

        Destroy(gameObject);
    }
}
