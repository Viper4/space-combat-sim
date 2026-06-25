using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class CraterFilter : IFilter
{
    FilterSettings.CraterSettings settings;
    List<(Vector3, float)> craters = new List<(Vector3, float)>();

    public CraterFilter(FilterSettings.CraterSettings settings)
    {
        this.settings = settings;
        craters = new List<(Vector3, float)>();
    }

    // Polynomial smooth max (Inigo Quilez). k = blend width, in the same
    // units as a/b. Cheap: one clamp, one lerp, two mults.
    private static float SmoothMax(float a, float b, float k)
    {
        float h = Mathf.Clamp01((a - b) / k + 0.5f);
        return Mathf.Lerp(b, a, h) + k * h * (1f - h);
    }

    private static float SamplePowerLaw(float rMin, float rMax, float alpha, float u)
    {
        // < 1 => More large craters
        // = 1 => Log-uniform (equal density per decade of size)
        // = 2 => Realistic with lunar/asteroid crater distribution
        // > 2 => More small craters
        if (Mathf.Approximately(alpha, 1f))
            return rMin * Mathf.Pow(rMax / rMin, u);

        float e = 1f - alpha;
        return Mathf.Pow(
            Mathf.Pow(rMin, e) + u * (Mathf.Pow(rMax, e) - Mathf.Pow(rMin, e)),
            1f / e
        );
    }

    public float Evaluate(Vector3 point)
    {
        if (craters.Count == 0)
        {
            for (int i = 0; i < settings.craters; i++)
            {
                float radius = SamplePowerLaw(settings.minCraterRadius, settings.maxCraterRadius, settings.sizeDistribution, Random.value);
                craters.Add((Random.onUnitSphere, radius));
            }
        }

        point = new Vector3(point.x * settings.scale.x, point.y * settings.scale.y, point.z * settings.scale.z);
        float height = 0f;

        foreach ((Vector3 center, float radius) in craters)
        {
            float maxInfluence = radius + settings.ridgeWidth;
            float sqrDistance = (center - point).sqrMagnitude;

            if (sqrDistance > maxInfluence * maxInfluence)
                continue; // cheap cull before sqrt

            float distance = Mathf.Sqrt(sqrDistance);
            float x = distance / radius;            // normalized: 0 at center, 1 at rim
            float w = settings.ridgeWidth / radius;  // normalized ridge width

            // bowl, with the floor-clamp corner rounded off
            float bowl = SmoothMax(x * x - 1f, -settings.craterFloor, settings.floorSmoothness) * radius * radius * settings.strength;

            // raised rim lip
            float ridgeRaw = x - 1f - w;
            float ridge = ridgeRaw * ridgeRaw * settings.ridgeHeight * settings.ridgeHeight * radius * radius;

            // blend the two across the rim instead of a hard branch
            float u = Mathf.InverseLerp(1f - settings.rimBlend, 1f + settings.rimBlend, x);
            float t = Mathf.SmoothStep(0f, 1f, u);
            height += Mathf.Lerp(bowl, ridge, Mathf.Clamp01(t + (Random.value - 0.5f) * 2f * settings.blendRandomness));
        }

        return height;
    }
}