using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PrefabField : MonoBehaviour
{
    Collider _collider;
    [SerializeField] GameObject prefab;
    [SerializeField] bool parent;
    [SerializeField] int numberOfObjects = 10;
    [SerializeField] int groupSize = 1;
    [SerializeField] [Range(0.0f, 1.0f)] float hollowStrength;

    void Start()
    {
        _collider = GetComponent<Collider>();
        for (int i = 0; i < numberOfObjects; i++)
        {
            if (parent)
                Instantiate(prefab, RandomPositionInCollider(), Random.rotation, transform);
            else
                Instantiate(prefab, RandomPositionInCollider(), Random.rotation);
        }
    }

    void Update()
    {
        
    }

    /// <summary>
    /// Returns random position inside any collider shape
    /// Assumes collider is convex and center is inside the collider
    /// </summary>
    /// <returns>Random position or Vector3.zero if no position found</returns>
    Vector3 RandomPositionInCollider()
    {
        // Shoot ray from max farthest distance to ensure we are outside of the mesh
        Ray ray = new Ray(transform.position, Random.insideUnitSphere.normalized);
        ray.origin = ray.GetPoint(-Vector3.Distance(transform.position, _collider.bounds.max) - 10);
        if(_collider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            Vector3 delta = transform.position - hit.point;
            return hit.point + Random.Range(0f, 1 - hollowStrength) * delta;
        }
        return Vector3.zero;
    }
}
