using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunk
{
    TerrainChunk[] children;
    ShapeGenerator shapeGenerator;
    Vector3 localPosition;
    float width;
    int maxLOD;
    int detailLevel;
    int filterResolution;
    int colliderResolution;
    Vector3 localUp;
    Vector3 localRight;
    Vector3 localForward;
    TerrainChunkObject chunkObject;

    public TerrainChunk(ShapeGenerator shapeGenerator, ShapeSettings settings, Vector3 localUp, int row, int col, int rootLOD)
    {
        this.shapeGenerator = shapeGenerator;
        filterResolution = settings.meshFilterResolution;
        colliderResolution = settings.complexMeshCollider ? settings.meshColliderResolution : ShapeSettings.simpleMeshColliderSize;
        width = 1 / rootLOD;
        maxLOD = settings.levelOfDetail;
        detailLevel = 0;
        this.localUp = localUp;
        localRight = new Vector3(localUp.y, localUp.z, localUp.x);
        localForward = Vector3.Cross(localUp, localRight);
        localPosition = localUp + (((2f * row + 1f) / rootLOD) - 1f) * localRight + (((2f * col + 1f) / rootLOD) - 1f) * localForward;
        localRight /= rootLOD;
        localForward /= rootLOD;
    }

    public TerrainChunk(ShapeGenerator shapeGenerator, Vector3 localPosition, float width, int filterResolution, int colliderResolution, int maxLOD, int detailLevel, Vector3 localUp, Vector3 localRight, Vector3 localForward)
    {
        this.shapeGenerator = shapeGenerator;
        this.localPosition = localPosition;
        this.width = width;
        this.filterResolution = filterResolution;
        this.colliderResolution = colliderResolution;
        this.maxLOD = maxLOD;
        this.detailLevel = detailLevel;
        this.localUp = localUp;
        this.localRight = localRight;
        this.localForward = localForward;
    }

    private Vector3 GetPointOnCubeSphere(Vector2 percent)
    {
        /*Vector3 pointOnUnitCube = localPosition + (2 * (percent.x - 0.5f) * localRight + 2 * (percent.y - 0.5f) * localForward);
        float x2 = pointOnUnitCube.x * pointOnUnitCube.x;
        float y2 = pointOnUnitCube.y * pointOnUnitCube.y;
        float z2 = pointOnUnitCube.z * pointOnUnitCube.z;
        Vector3 pointOnUnitSphere;
        pointOnUnitSphere.x = pointOnUnitCube.x * Mathf.Sqrt(1f - y2 / 2f - z2 / 2f + y2 * z2 / 3f);
        pointOnUnitSphere.y = pointOnUnitCube.y * Mathf.Sqrt(1f - x2 / 2f - z2 / 2f + x2 * z2 / 3f);
        pointOnUnitSphere.z = pointOnUnitCube.z * Mathf.Sqrt(1f - x2 / 2f - y2 / 2f + x2 * y2 / 3f);
        return pointOnUnitSphere;*/

        Vector3 pointOnUnitCube = localPosition + (2f * (percent.x - 0.5f) * localRight + 2f * (percent.y - 0.5f) * localForward);
        return pointOnUnitCube.normalized;
    }

    public void ConstructMesh(Camera camera = null, bool forceMeshCollider = false)
    {
        Mesh filterMesh = chunkObject.meshFilter.sharedMesh;
        Vector3[] vertices = new Vector3[filterResolution * filterResolution]; // resolution vertices on each side of the mesh
        int[] triangles = new int[(filterResolution - 1) * (filterResolution - 1) * 6]; // 2 triangles per square so (resolution - 1)^2 faces * 2 tris * 3 vertices per tri
        int triangleIndex = 0;
        Vector2[] uv = filterMesh.uv;

        int i = 0;
        for (int y = 0; y < filterResolution; y++)
        {
            for (int x = 0; x < filterResolution; x++)
            {
                vertices[i] = shapeGenerator.CalculatePointOnSphere(GetPointOnCubeSphere(new Vector2(x, y) / (filterResolution - 1)));

                if (x != filterResolution - 1 && y != filterResolution - 1)
                {
                    triangles[triangleIndex] = i;
                    triangles[triangleIndex + 1] = i + filterResolution + 1;
                    triangles[triangleIndex + 2] = i + filterResolution;

                    triangles[triangleIndex + 3] = i;
                    triangles[triangleIndex + 4] = i + 1;
                    triangles[triangleIndex + 5] = i + filterResolution + 1;
                    triangleIndex += 6;
                }
                i++;
            }
        }

        filterMesh.Clear();
        filterMesh.vertices = vertices;
        filterMesh.triangles = triangles;
        filterMesh.RecalculateNormals();
        if (filterMesh.uv.Length == uv.Length)
            filterMesh.uv = uv;

        bool generateCollider = forceMeshCollider || (camera != null && chunkObject.meshRenderer.bounds.SqrDistance(camera.transform.position) < 100);
        if (generateCollider)
            ConstructMeshCollider();
        else
            chunkObject.UpdateBoxCollider();            
    }

    public void ConstructMeshCollider()
    {
        if(chunkObject.meshCollider == null)
        {
            chunkObject.meshCollider = chunkObject.meshFilter.gameObject.AddComponent<MeshCollider>();
            chunkObject.meshCollider.sharedMesh = new Mesh();
        }
        Mesh colliderMesh = chunkObject.meshCollider.sharedMesh;
        Vector3[] vertices = new Vector3[colliderResolution * colliderResolution];
        int[] triangles = new int[(colliderResolution - 1) * (colliderResolution - 1) * 6];
        int triangleIndex = 0;

        int i = 0;
        for (int y = 0; y < colliderResolution; y++)
        {
            for (int x = 0; x < colliderResolution; x++)
            {
                vertices[i] = shapeGenerator.CalculatePointOnSphere(GetPointOnCubeSphere(new Vector2(x, y) / (colliderResolution - 1)));

                if (x != colliderResolution - 1 && y != colliderResolution - 1)
                {
                    triangles[triangleIndex] = i;
                    triangles[triangleIndex + 1] = i + colliderResolution + 1;
                    triangles[triangleIndex + 2] = i + colliderResolution;

                    triangles[triangleIndex + 3] = i;
                    triangles[triangleIndex + 4] = i + 1;
                    triangles[triangleIndex + 5] = i + colliderResolution + 1;
                    triangleIndex += 6;
                }
                i++;
            }
        }

        colliderMesh.Clear();
        colliderMesh.vertices = vertices;
        colliderMesh.triangles = triangles;
        colliderMesh.RecalculateNormals();
        chunkObject.meshCollider.convex = true;
    }

    public void UpdateUVs(ColorGenerator colorGenerator)
    {
        Vector2[] uv = new Vector2[filterResolution * filterResolution];
        int i = 0;
        for (int y = 0; y < filterResolution; y++)
        {
            for (int x = 0; x < filterResolution; x++)
            {
                Vector3 vertexPosition = GetPointOnCubeSphere(new Vector2(x, y) / (filterResolution - 1));
                uv[i] = new Vector2(x, y);
                //uv[i] = new Vector2(colorGenerator.BiomePercentFromPoint(GetPointOnCubeSphere(new Vector2(x, y) / (filterResolution - 1))), 0);
                i++;
            }
        }
        chunkObject.meshFilter.sharedMesh.uv = uv;
    }

    public void GenerateTree(Camera camera, Transform parent, ColorGenerator colorGenerator)
    {
        float sqrDistance = 0;
        if(camera != null)
            sqrDistance = chunkObject == null ? (parent.TransformPoint(shapeGenerator.CalculatePointOnSphere(GetPointOnCubeSphere(new Vector2(0.5f, 0.5f)))) - camera.transform.position).sqrMagnitude : chunkObject.meshRenderer.bounds.SqrDistance(camera.transform.position);
        if (camera != null && detailLevel >= 0 && detailLevel < maxLOD && sqrDistance < width * width)
        {
            if (chunkObject != null)
                chunkObject.gameObject.SetActive(false);
            children = new TerrainChunk[4];
            float nextWidth = width * 0.5f;
            Vector3 nextLocalRight = localRight * 0.5f;
            Vector3 nextLocalForward = localForward * 0.5f;
            children[0] = new TerrainChunk(shapeGenerator, localPosition + nextLocalRight - nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Top left
            children[1] = new TerrainChunk(shapeGenerator, localPosition + nextLocalRight + nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Top right
            children[2] = new TerrainChunk(shapeGenerator, localPosition - nextLocalRight + nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Bottom right
            children[3] = new TerrainChunk(shapeGenerator, localPosition - nextLocalRight - nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Bottom left

            foreach (TerrainChunk chunk in children)
            {
                chunk.GenerateTree(camera, parent, colorGenerator);
            }
        }
        else
        {
            if (chunkObject == null)
            {
                GameObject meshGO = new GameObject("Mesh (" + detailLevel + " LOD)");
                meshGO.transform.SetParent(parent, false);
                chunkObject = meshGO.AddComponent<TerrainChunkObject>();
                chunkObject.Init(this);
                chunkObject.gameObject.layer = parent.gameObject.layer;
            }

            chunkObject.gameObject.SetActive(true);
            chunkObject.meshRenderer.sharedMaterial = colorGenerator.materialInstance;
            chunkObject.meshFilter.sharedMesh = new Mesh();
            if (camera != null)
            {
                ConstructMesh(camera);
                UpdateUVs(colorGenerator);
            }
        }
    }

    /* 
     * Doesn't generate chunks already created and enables mesh depending on if mesh is visible by camera
     */
    public void UpdateTree(Camera camera, Transform parent, ColorGenerator colorGenerator)
    {
        float sqrDistance = 0;
        if (camera != null)
            sqrDistance = chunkObject == null ? (parent.TransformPoint(shapeGenerator.CalculatePointOnSphere(GetPointOnCubeSphere(new Vector2(0.5f, 0.5f)))) - camera.transform.position).sqrMagnitude : chunkObject.meshRenderer.bounds.SqrDistance(camera.transform.position);
        if (camera != null && detailLevel >= 0 && detailLevel < maxLOD && sqrDistance < width * width)
        {
            if(chunkObject != null)
                chunkObject.gameObject.SetActive(false);
            if (children == null || children.Length == 0)
            {
                children = new TerrainChunk[4];
                float nextWidth = width * 0.5f;
                Vector3 nextLocalRight = localRight * 0.5f;
                Vector3 nextLocalForward = localForward * 0.5f;
                children[0] = new TerrainChunk(shapeGenerator, localPosition + nextLocalRight - nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Top left
                children[1] = new TerrainChunk(shapeGenerator, localPosition + nextLocalRight + nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Top right
                children[2] = new TerrainChunk(shapeGenerator, localPosition - nextLocalRight + nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Bottom right
                children[3] = new TerrainChunk(shapeGenerator, localPosition - nextLocalRight - nextLocalForward, nextWidth, filterResolution, colliderResolution, maxLOD, detailLevel + 1, localUp, nextLocalRight, nextLocalForward); // Bottom left
            }

            foreach (TerrainChunk chunk in children)
            {
                chunk.UpdateTree(camera, parent, colorGenerator);
            }
        }
        else
        {
            if (chunkObject == null)
            {
                GameObject meshGO = new GameObject("Mesh (" + detailLevel + " LOD)");
                meshGO.transform.SetParent(parent, false);
                chunkObject = meshGO.AddComponent<TerrainChunkObject>();
                chunkObject.Init(this);
                chunkObject.gameObject.layer = parent.gameObject.layer;
                chunkObject.meshFilter.sharedMesh = new Mesh();
                chunkObject.meshRenderer.sharedMaterial = colorGenerator.materialInstance;
                ConstructMesh(camera);
                UpdateUVs(colorGenerator);
            }
            chunkObject.gameObject.SetActive(true);
        }
    }
}
