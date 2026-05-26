using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PerlinNoiseFilter : IFilter
{
    FilterSettings.PerlinNoiseSettings settings;

    public PerlinNoiseFilter(FilterSettings.PerlinNoiseSettings settings)
    {
        this.settings = settings;
    }

    public float Evaluate(Vector3 point)
    {
        point = new Vector3(point.x * settings.scale.x, point.y * settings.scale.y, point.z * settings.scale.z);
        float noiseValue = 0;
        float frequency = settings.baseRoughness;
        float amplitude = 1;

        for (int i = 0; i < settings.layers; i++)
        {
            float value = frequency * Noise3D(point + settings.seed);
            noiseValue += (value + 1) * 0.5f * amplitude;
            frequency *= settings.roughness;
            amplitude *= settings.persistence;
        }
        noiseValue = Mathf.Max(0, noiseValue - settings.minValue);
        return noiseValue * settings.strength;
    }

    private float Noise3D(Vector3 point)
    {
        // Symmetry at seeds within 1, 1, 1 of 0, 0, 0
        float xy = Mathf.PerlinNoise(point.x, point.y);
        float xz = Mathf.PerlinNoise(point.x, point.z);
        float yz = Mathf.PerlinNoise(point.y, point.z);
        float yx = Mathf.PerlinNoise(point.y, point.x);
        float zx = Mathf.PerlinNoise(point.z, point.x);
        float zy = Mathf.PerlinNoise(point.z, point.y);
        return (xy + xz + yz + yx + zx + zy) / 6;
    }
}
