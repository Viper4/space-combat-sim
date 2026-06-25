using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    [SerializeField] private Vector2 seedRange = new Vector2(-999, 999);

    public bool initialized {get; private set;} = false;
    public bool generated {get; private set;} = false;

    private ScaledTransform scaledTransform;

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
        rootChunks = null;
        generated = false;
    }

    private void Init()
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

        DestroyGeneratedChunks();

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

        initialized = true;
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

    public void GenerateCelestialBody()
    {
        Init();
        GenerateMeshes();
        GenerateColors();
        generated = true;
    }

    public void GenerateRandomCelestialBody()
    {
        Init();
        foreach (var noiseLayer in shapeSettings.filterLayers)
        {
            switch (noiseLayer.filterSettings.filterType)
            {
                case FilterSettings.FilterType.Simplex:
                    noiseLayer.filterSettings.simplexNoiseSettings.seed = RandomSeed();
                    break;
                case FilterSettings.FilterType.Ridge:
                    noiseLayer.filterSettings.ridgeNoiseSettings.seed = RandomSeed();
                    break;
                case FilterSettings.FilterType.Perlin:
                    noiseLayer.filterSettings.perlinNoiseSettings.seed = RandomSeed();
                    break;
                case FilterSettings.FilterType.Crater:
                    noiseLayer.filterSettings.craterSettings.seed = RandomSeed();
                    break;
            }
        }
        switch (colorSettings.biomeColorSettings.filter.filterType)
        {
            case FilterSettings.FilterType.Simplex:
                colorSettings.biomeColorSettings.filter.simplexNoiseSettings.seed = RandomSeed();
                break;
            case FilterSettings.FilterType.Ridge:
                colorSettings.biomeColorSettings.filter.ridgeNoiseSettings.seed = RandomSeed();
                break;
            case FilterSettings.FilterType.Perlin:
                colorSettings.biomeColorSettings.filter.perlinNoiseSettings.seed = RandomSeed();
                break;
            case FilterSettings.FilterType.Crater:
                colorSettings.biomeColorSettings.filter.craterSettings.seed = RandomSeed();
                break;
        }
        GenerateMeshes();
        GenerateColors();
        generated = true;
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
            Init();
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
}
