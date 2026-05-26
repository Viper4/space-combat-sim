using System.Collections.Generic;
using UnityEngine;

public class CraterFilter : IFilter
{
    FilterSettings.CraterSettings settings;

    Dictionary<Vector3, float> craters = new Dictionary<Vector3, float>();

    public CraterFilter(FilterSettings.CraterSettings settings)
    {
        this.settings = settings;
        craters = new Dictionary<Vector3, float>();
    }

    public float Evaluate(Vector3 point)
    {
        if(craters.Count == 0)
        {
            for (int i = 0; i < settings.craters; i++)
            {
                float y = Mathf.Pow(Random.value, settings.sizeDistribution);
                craters.Add(Random.onUnitSphere, Mathf.Max(settings.minCraterRadius, y * settings.maxCraterRadius));
            }
        }

        point = new Vector3(point.x * settings.scale.x, point.y * settings.scale.y, point.z * settings.scale.z);
        float height = 0;
        foreach (KeyValuePair<Vector3, float> crater in craters)
        {
            float sqrDistance = (crater.Key - point).sqrMagnitude;
            float radius = crater.Value;
            float sqrRadius = radius * radius;
            if (sqrDistance <= sqrRadius)
            {
                height += Mathf.Max(-sqrRadius * settings.craterFloor, sqrDistance - sqrRadius) * settings.strength;
            }
            else if (sqrDistance <= (radius + settings.ridgeWidth) * (radius + settings.ridgeWidth))
            {
                float ridge = (Mathf.Sqrt(sqrDistance) - settings.ridgeWidth - radius) * settings.ridgeHeight;
                ridge *= ridge;
                height += ridge;
            }
        }
        return height;
    }
}
