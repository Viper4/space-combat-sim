using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ValleyNoiseFilter : IFilter
{
    Noise noise = new Noise();
    FilterSettings.ValleyNoiseSettings settings;

    public ValleyNoiseFilter(FilterSettings.ValleyNoiseSettings settings)
    {
        this.settings = settings;
    }

    public float Evaluate(Vector3 point)
    {
        point = new Vector3(point.x * settings.scale.x, point.y * settings.scale.y, point.z * settings.scale.z);
        float noiseValue = 0;
        float frequency = settings.baseRoughness;
        float amplitude = 1;
        float weight = 1;

        for (int i = 0; i < settings.layers; i++)
        {
            float value = Mathf.Abs(noise.Evaluate(point * frequency + settings.seed)); // Visualize noise function as a sin wave. |sin(x)| gives a graph with sharp valleys
            value *= weight;
            weight = Mathf.Clamp01(value * settings.weightMultiplier);

            noiseValue += value * amplitude;
            frequency *= settings.roughness;
            amplitude *= settings.persistence;
        }
        noiseValue = Mathf.Max(0, noiseValue - settings.minValue);
        return noiseValue * settings.strength;
    }
}
