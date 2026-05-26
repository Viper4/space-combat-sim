using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShapeGenerator
{
    public ShapeSettings settings { get; private set; }
    IFilter[] filters;
    public MinMax elevationMinMax;

    public void UpdateSettings(ShapeSettings settings)
    {
        this.settings = settings;
        filters = new IFilter[settings.filterLayers.Length];
        for (int i = 0; i < filters.Length; i++)
        {
            filters[i] = FilterCreator.CreateFilter(settings.filterLayers[i].filterSettings);
        }
        elevationMinMax = new MinMax();
    }

    public Vector3 CalculatePointOnSphere(Vector3 pointOnUnitSphere)
    {
        float firstLayerValue = 0;
        float elevation = 0;

        if (filters.Length > 0)
        {
            firstLayerValue = filters[0].Evaluate(pointOnUnitSphere);
            if (settings.filterLayers[0].enabled)
            {
                elevation = firstLayerValue;
            }
        }

        for (int i = 1; i < filters.Length; i++)
        {
            if (settings.filterLayers[i].enabled)
            {
                float mask = settings.filterLayers[i].useFirstLayerAsMask ? firstLayerValue : 1;
                elevation += filters[i].Evaluate(pointOnUnitSphere) * mask;
            }
        }
        elevation = (1 + elevation) * settings.radius;
        elevationMinMax.AddValue(elevation);
        return elevation * pointOnUnitSphere;
    }
}
