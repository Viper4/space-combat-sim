using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;

public class Bullet : Projectile
{
    [SerializeField] private float damageAmount = 10;

    private void OnCollisionEnter(Collision collision)
    {
        switch(collision.transform.tag)
        {
            case "Ship":
                Ship ship = collision.transform.GetComponent<Ship>();
                ship.statSystem.Damage(damageAmount);
                
                //ship.PlayBulletEffect(collision.GetContact(0).point, );
                break;
            case "Torpedo":
                collision.transform.GetComponent<Torpedo>().Detonate(collision.GetContact(0).point);
                break;
            case "Shields":
                collision.transform.GetComponent<Shields>().Damage(damageAmount, collision.GetContact(0).point);
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
