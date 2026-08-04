using System;
using SpaceStuff;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(ScaledTransform))]
public class CelestialBodyGenerator : MonoBehaviour
{
    public bool autoUpdate = true;
    public enum FaceRenderMask
    {
        All,
        Top,
        Bottom,
        Left,
        Right,
        Front,
        Back
    }
    public FaceRenderMask renderMask;

    public ShapeSettings originalShapeSettings;
    private ShapeSettings shapeSettings;
    public ColorSettings originalColorSettings;
    private ColorSettings colorSettings;
    [SerializeField] private bool cloneSettings;

    [HideInInspector] public bool shapeSettingsFoldout;
    [HideInInspector] public bool colorSettingsFoldout;

    [SerializeField, Tooltip("Base LOD will be displayed at screen sizes below this.")] private float baseMaxScreenSize;

    private ShapeGenerator shapeGenerator;
    private ColorGenerator colorGenerator;

    [SerializeField, Range(1, 32)] private int rootLOD = 1;
    private TerrainChunk[] rootChunks;

    [SerializeField] private bool randomSeed;
    [SerializeField] private Vector2 seedRange = new Vector2(-999, 999);

    public bool generated {get; private set;} = false;

    private ScaledTransform scaledTransform;

    private Vector3[] currentSeeds = null;
    
    private void InitRootChunks()
    {
        rootChunks = new TerrainChunk[6 * rootLOD * rootLOD];

        Vector3[] directions = new Vector3[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        int arrayIndex = 0;
        for (int i = 0; i < 6; i++)
        {
            for (int r = 0; r < rootLOD; r++)
            {
                for (int c = 0; c < rootLOD; c++)
                {
                    rootChunks[arrayIndex] = new TerrainChunk(shapeGenerator, shapeSettings, baseMaxScreenSize, directions[i], r, c, rootLOD);
                    if (renderMask == FaceRenderMask.All || (int)renderMask - 1 == i)
                        rootChunks[arrayIndex].GenerateEmptyTree(transform, colorGenerator);
                    arrayIndex++;
                }
            }
        }
    }

    public void Init(Vector3[] seeds = null)
    {
        shapeGenerator = new ShapeGenerator();
        colorGenerator = new ColorGenerator();
        scaledTransform = GetComponent<ScaledTransform>();
        Material bodyMaterial;
        if (cloneSettings)
        {
            shapeSettings = Instantiate(originalShapeSettings);
            colorSettings = Instantiate(originalColorSettings);
            bodyMaterial = Instantiate(colorSettings.material);
        }
        else
        {
            shapeSettings = originalShapeSettings;
            colorSettings = originalColorSettings;
            bodyMaterial = colorSettings.material;
        }
        Vector3 floatRealScale = scaledTransform.realScale.ToVector3();
        foreach (ShapeSettings.FilterLayer filterLayer in shapeSettings.filterLayers)
        {
            if (filterLayer.applyScale)
            {
                filterLayer.filterSettings.simplexNoiseSettings.scale = floatRealScale;
                filterLayer.filterSettings.ridgeNoiseSettings.scale = floatRealScale;
                filterLayer.filterSettings.perlinNoiseSettings.scale = floatRealScale;
                filterLayer.filterSettings.craterSettings.scale = floatRealScale;
            }
        }

        shapeGenerator.UpdateSettings(shapeSettings);
        colorGenerator.UpdateSettings(colorSettings, bodyMaterial);

        InitRootChunks();

        if (seeds != null)
        {
            currentSeeds = seeds;
            return;
        }

        currentSeeds = new Vector3[shapeSettings.filterLayers.Length + 1];
        if (randomSeed)
        {
            RandomizeSeeds();
            return;
        }

        for (int i = 0; i < shapeSettings.filterLayers.Length; i++)
        {
            ShapeSettings.FilterLayer noiseLayer = shapeSettings.filterLayers[i];
            switch (noiseLayer.filterSettings.filterType)
            {
                case FilterSettings.FilterType.Simplex:
                    currentSeeds[i] = noiseLayer.filterSettings.simplexNoiseSettings.seed;
                    break;
                case FilterSettings.FilterType.Ridge:
                    currentSeeds[i] = noiseLayer.filterSettings.ridgeNoiseSettings.seed;
                    break;
                case FilterSettings.FilterType.Perlin:
                    currentSeeds[i] = noiseLayer.filterSettings.perlinNoiseSettings.seed;
                    break;
                case FilterSettings.FilterType.Crater:
                    currentSeeds[i] = noiseLayer.filterSettings.craterSettings.seed;
                    break;
            }
        }
        switch (colorSettings.biomeColorSettings.filter.filterType)
        {
            case FilterSettings.FilterType.Simplex:
                currentSeeds[^1] = colorSettings.biomeColorSettings.filter.simplexNoiseSettings.seed;
                break;
            case FilterSettings.FilterType.Ridge:
                currentSeeds[^1] = colorSettings.biomeColorSettings.filter.ridgeNoiseSettings.seed;
                break;
            case FilterSettings.FilterType.Perlin:
                currentSeeds[^1] = colorSettings.biomeColorSettings.filter.perlinNoiseSettings.seed;
                break;
            case FilterSettings.FilterType.Crater:
                currentSeeds[^1] = colorSettings.biomeColorSettings.filter.craterSettings.seed;
                break;
        }
    }

    public void DestroyGeneratedChunks()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.Contains("Mesh"))
            {
                if (Application.isEditor)
                    DestroyImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
            }
        }
        generated = false;
        rootChunks = null;
    }

    public bool UpdateQuadTrees(Camera camera)
    {
        bool updateGenerated = false;
        Vector3d realCamPos = FloatingWorldOrigin.Instance.scaledTransform.realPosition + camera.transform.position.ToVector3d();

        foreach (TerrainChunk rootChunk in rootChunks)
        {
            updateGenerated |= rootChunk.UpdateTree(realCamPos, scaledTransform, colorGenerator);
        }
        return updateGenerated;
    }

    public void GenerateCelestialBody(bool randomizeSeeds = false, bool inEditor = false)
    {
        if (!inEditor && currentSeeds == null)
            return;
        if (randomizeSeeds)
            RandomizeSeeds();
        if (rootChunks == null)
            InitRootChunks();
        GenerateMeshes();
        GenerateColors();
        generated = true;
    }

    private void UpdateFilterSeeds()
    {
        for (int i = 0; i < shapeSettings.filterLayers.Length; i++)
        {
            ShapeSettings.FilterLayer noiseLayer = shapeSettings.filterLayers[i];
            switch (noiseLayer.filterSettings.filterType)
            {
                case FilterSettings.FilterType.Simplex:
                    noiseLayer.filterSettings.simplexNoiseSettings.seed = currentSeeds[i];
                    break;
                case FilterSettings.FilterType.Ridge:
                    noiseLayer.filterSettings.ridgeNoiseSettings.seed = currentSeeds[i];
                    break;
                case FilterSettings.FilterType.Perlin:
                    noiseLayer.filterSettings.perlinNoiseSettings.seed = currentSeeds[i];
                    break;
                case FilterSettings.FilterType.Crater:
                    noiseLayer.filterSettings.craterSettings.seed = currentSeeds[i];
                    break;
            }
        }
        switch (colorSettings.biomeColorSettings.filter.filterType)
        {
            case FilterSettings.FilterType.Simplex:
                colorSettings.biomeColorSettings.filter.simplexNoiseSettings.seed = currentSeeds[^1];
                break;
            case FilterSettings.FilterType.Ridge:
                colorSettings.biomeColorSettings.filter.ridgeNoiseSettings.seed = currentSeeds[^1];
                break;
            case FilterSettings.FilterType.Perlin:
                colorSettings.biomeColorSettings.filter.perlinNoiseSettings.seed = currentSeeds[^1];
                break;
            case FilterSettings.FilterType.Crater:
                colorSettings.biomeColorSettings.filter.craterSettings.seed = currentSeeds[^1];
                break;
        }
    }

    private void RandomizeSeeds()
    {
        for(int i = 0; i < currentSeeds.Length; i++)
        {
            currentSeeds[i] = RandomSeed();
        }
        UpdateFilterSeeds();
    }

    private Vector3 RandomSeed()
    {
        return new Vector3(Random.Range(seedRange.x, seedRange.y), Random.Range(seedRange.x, seedRange.y), Random.Range(seedRange.x, seedRange.y));
    }

    public void OnShapeSettingsUpdated()
    {
        if (autoUpdate)
        {
            GenerateCelestialBody(); // Meshes get reset when changing LOD or mesh size
        }
    }

    public void OnColorSettingsUpdated()
    {
        if (autoUpdate)
        {
            GenerateColors();
        }
    }

    private void GenerateMeshes()
    {
        for (int i = 0; i < 6 * rootLOD * rootLOD; i++)
        {
            int faceIndex = i / (rootLOD * rootLOD);
            if (renderMask == FaceRenderMask.All || (int)renderMask - 1 == faceIndex)
                rootChunks[i].ConstructMesh();
            if (shapeSettings.meshColliderResolution > 0)
                rootChunks[i].ConstructMeshCollider();
        }
        colorGenerator.UpdateElevation(shapeGenerator.elevationMinMax);
    }

    /* TODO:
     * Add option to apply coloring based on random noise instead of elevation and/or biome
     */
    public void GenerateColors()
    {
        colorGenerator.UpdateColors();
        for (int i = 0; i < 6 * rootLOD * rootLOD; i++)
        {
            int faceIndex = i / (rootLOD * rootLOD);
            if (renderMask == FaceRenderMask.All || (int)renderMask - 1 == faceIndex)
                rootChunks[i].UpdateUVs(colorGenerator);
        }
    }

    public Vector3[] GetSeeds()
    {
        return currentSeeds;
    }

    public void SetSeeds(Vector3[] seeds)
    {
        DestroyGeneratedChunks();
        currentSeeds = seeds;
        UpdateFilterSeeds();
    }
}
