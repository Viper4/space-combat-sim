using System.Collections;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

public class Bullet : Projectile
{
    [SerializeField] private float damageAmount = 10;

    protected override void OnCollide(ScaledSpacePhysics.CollisionInfo collisionInfo)
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
}
