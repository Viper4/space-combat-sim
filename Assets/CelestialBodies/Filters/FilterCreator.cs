using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FilterCreator
{
    public static IFilter CreateFilter(FilterSettings settings)
    {
        return settings.filterType switch
        {
            FilterSettings.FilterType.Simplex => new SimplexNoiseFilter(settings.simplexNoiseSettings),
            FilterSettings.FilterType.Ridge => new RidgeNoiseFilter(settings.ridgeNoiseSettings),
            FilterSettings.FilterType.Perlin => new PerlinNoiseFilter(settings.perlinNoiseSettings),
            FilterSettings.FilterType.Crater => new CraterFilter(settings.craterSettings),
            FilterSettings.FilterType.Valley => new ValleyNoiseFilter(settings.valleyNoiseSettings),
            _ => null,
        };
    }
}
