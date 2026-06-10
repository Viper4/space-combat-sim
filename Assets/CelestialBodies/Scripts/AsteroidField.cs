using System;
using SpaceStuff;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections;

public class AsteroidField : MonoBehaviour
{
    [SerializeField] private GameObject asteroidPrefab;
    [SerializeField] private CelestialBody orbitTarget;
    [SerializeField, Tooltip("Distance to inner edge of the torus in AUs.")] private double innerRadius = 2.06;
    [SerializeField, Tooltip("Distance to outer edge of the torus in AUs.")] private double outerRadius = 3.28;
    [SerializeField, Tooltip("Vertical thickness of the torus from bottom to top in AUs.")] private double thickness = 1.0;
    [SerializeField] private int numberOfObjects = 100000;

    private IEnumerator Start()
    {
        for (int i = 0; i < numberOfObjects; i++)
        {
            GameObject obj = Instantiate(asteroidPrefab, transform);

            if (!obj.TryGetComponent<CelestialBody>(out var asteroid))
            {
                Debug.LogError("Prefab does not have a CelestialBody, cannot generate asteroid field.");
                Destroy(obj);
                yield break;
            }
            asteroid.pauseUpdates = true;
            yield return new WaitUntil(asteroid.Initialized);

            asteroid.scaledTransform.realPosition = GetRandomPointInTorus();
            asteroid.transform.rotation = Random.rotation;
            asteroid.SetOrbit(orbitTarget);
            asteroid.pauseUpdates = false;
        }
    }

    private Vector3d GetRandomPointInTorus()
    {
        Vector3d center = orbitTarget.scaledTransform.realPosition;

        double theta = Random.value * Math.PI * 2.0;

        double r = Math.Sqrt(Random.value);
        double radius = innerRadius + (outerRadius - innerRadius) * r;

        double x = Math.Cos(theta) * radius * SpaceMath.astronomicalUnit;
        double z = Math.Sin(theta) * radius * SpaceMath.astronomicalUnit;

        double y = NextGaussian(0.0, thickness * 0.25) * SpaceMath.astronomicalUnit;

        return center + new Vector3d(x, y, z);
    }

    private double NextGaussian(double mean = 0.0, double stdDev = 1.0)
    {
        double u1 = 1.0 - Random.value;
        double u2 = 1.0 - Random.value;

        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        return mean + stdDev * randStdNormal;
    }
}
