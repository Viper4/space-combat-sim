using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorGenerator
{
    public ColorSettings settings { get; private set; }
    Texture2D texture;
    public Material materialInstance;
    const int textureResolution = 50;
    IFilter biomeFilter;

    public void UpdateSettings(ColorSettings settings, Material materialInstance)
    {
        this.settings = settings;
        if(texture == null || texture.height != settings.biomeColorSettings.biomes.Length)
        {
            texture = new Texture2D(textureResolution, settings.biomeColorSettings.biomes.Length, TextureFormat.RGBA32, false);
        }
        this.materialInstance = materialInstance;
        biomeFilter = FilterCreator.CreateFilter(settings.biomeColorSettings.filter);
    }

    public void UpdateElevation(MinMax elevationMinMax)
    {
        if(materialInstance != null)
            materialInstance.SetVector("_ElevationMinMax", new Vector4(elevationMinMax.Min, elevationMinMax.Max));
    }

    // At biomes >= 4 the colors of the biomes are "foggy" at low blend values
    public float BiomePercentFromPoint(Vector3 pointOnUnitSphere)
    {
        float heightPercent = (pointOnUnitSphere.y + 1) * 0.5f;
        heightPercent += (biomeFilter.Evaluate(pointOnUnitSphere) - settings.biomeColorSettings.offset) * settings.biomeColorSettings.strength;
        float biomeIndex = 0;
        int numBiomes = settings.biomeColorSettings.biomes.Length;
        float blendRange = settings.biomeColorSettings.blend * 0.5f + 0.001f;

        for (int i = 0; i < numBiomes; i++)
        {
            float distance = heightPercent - settings.biomeColorSettings.biomes[i].startHeight;
            float weight = Mathf.InverseLerp(-blendRange, blendRange, distance) * settings.biomeColorSettings.biomes[i].weightMultiplier;
            biomeIndex *= 1 - weight;
            biomeIndex += i * weight;
        }
        return biomeIndex / Mathf.Max(1, numBiomes - 1);
    }

    public void UpdateColors()
    {
        if(materialInstance != null)
        {
            Color[] colors = new Color[texture.width * texture.height];
            int colorIndex = 0;
            foreach (ColorSettings.BiomeColorSettings.Biome biome in settings.biomeColorSettings.biomes)
            {
                Color tintColor = biome.tint;
                for (int i = 0; i < textureResolution; i++)
                {
                    Color gradientColor = biome.gradient.Evaluate(i / (textureResolution - 1f));
                    colors[colorIndex] = gradientColor * (1 - biome.tintPercent) + tintColor * biome.tintPercent;
                    colorIndex++;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            materialInstance.SetTexture("_MainTexture", texture);
        }
    }
}
