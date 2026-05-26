using UnityEngine;

[RequireComponent(typeof(DoubleRigidbody))]
public class Projectile : MonoBehaviour
{
    protected DoubleRigidbody doubleRigidbody;

    [SerializeField, Header("Projectile")] protected LayerMask ignoreLayers;

    [SerializeField] private float destroyDelay = 5;

    protected virtual void Start()
    {
        doubleRigidbody = GetComponent<DoubleRigidbody>();
        if(destroyDelay > 0)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}
