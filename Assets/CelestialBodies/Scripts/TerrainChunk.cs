using SpaceStuff;
using UnityEngine;

public class TerrainChunk
{
    private TerrainChunk[] children;
    private ShapeGenerator shapeGenerator;
    private Vector3 localPosition;
    private float width;
    // This terrain chunk will be visible below max screen size, anything above will generate higher LOD
    private float sqrMaxScreenSize;
    private int maxLOD;
    private int detailLevel;
    private int filterResolution;
    private int colliderResolution;
    private Vector3 localUp;
    private Vector3 localRight;
    private Vector3 localForward;
    private TerrainChunkObject chunkObject;
    private Vector3[] vertices;

    public TerrainChunk(ShapeGenerator shapeGenerator, ShapeSettings settings, float maxScreenSize, Vector3 localUp, int row, int col, int rootLOD)
    {
        this.shapeGenerator = shapeGenerator;
        filterResolution = settings.meshFilterResolution;
        colliderResolution = settings.meshColliderResolution;
        width = 1f / rootLOD;
        sqrMaxScreenSize = maxScreenSize * maxScreenSize;
        maxLOD = settings.levelOfDetail;
        detailLevel = 0;
        this.localUp = localUp;
        localRight = new Vector3(localUp.y, localUp.z, localUp.x);
        localForward = Vector3.Cross(localUp, localRight);
        localPosition = localUp + (((2f * row + 1f) / rootLOD) - 1f) * localRight + (((2f * col + 1f) / rootLOD) - 1f) * localForward;
        localRight /= rootLOD;
        localForward /= rootLOD;
    }

    public TerrainChunk(ShapeGenerator shapeGenerator, Vector3 localPosition, float width, float sqrMaxScreenSize, int filterResolution, int colliderResolution, int maxLOD, int detailLevel, Vector3 localUp, Vector3 localRight, Vector3 localForward)
    {
        this.shapeGenerator = shapeGenerator;
        this.localPosition = localPosition;
        this.width = width;
        this.sqrMaxScreenSize = sqrMaxScreenSize;
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

    public void ConstructMesh()
    {
        if (chunkObject == null)
            return;
        Mesh filterMesh = chunkObject.meshFilter.sharedMesh; // Cant get shared mesh outside main thread
        vertices = new Vector3[filterResolution * filterResolution]; // resolution vertices on each side of the mesh
        int[] triangles = new int[(filterResolution - 1) * (filterResolution - 1) * 6]; // 2 triangles per square so (resolution - 1)^2 faces * 2 tris * 3 vertices per tri
        int triangleIndex = 0;
        Vector2[] uv = filterMesh.uv;

        int i = 0;
        for (int y = 0; y < filterResolution; y++)
        {
            for (int x = 0; x < filterResolution; x++)
            {
                vertices[i] = shapeGenerator.EvaluatePointOnSphere(GetPointOnCubeSphere(new Vector2(x, y) / (filterResolution - 1)));

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

        if (filterMesh == null)
            return;

        filterMesh.Clear();
        filterMesh.vertices = vertices;
        filterMesh.triangles = triangles;
        filterMesh.RecalculateNormals();
        if (filterMesh.uv.Length == uv.Length)
            filterMesh.uv = uv;
    }

    public void ConstructMeshCollider()
    {
        if(chunkObject.meshCollider == null)
        {
            chunkObject.meshCollider = chunkObject.meshFilter.gameObject.AddComponent<MeshCollider>();
            chunkObject.meshCollider.sharedMesh = new Mesh();
        }
        Mesh colliderMesh = chunkObject.meshCollider.sharedMesh;
        Vector3[] colliderVertices = new Vector3[colliderResolution * colliderResolution];
        int[] triangles = new int[(colliderResolution - 1) * (colliderResolution - 1) * 6];
        int triangleIndex = 0;

        int i = 0;
        for (int y = 0; y < colliderResolution; y++)
        {
            for (int x = 0; x < colliderResolution; x++)
            {
                colliderVertices[i] = shapeGenerator.EvaluatePointOnSphere(GetPointOnCubeSphere(new Vector2(x, y) / (colliderResolution - 1)));

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
        colliderMesh.vertices = colliderVertices;
        colliderMesh.triangles = triangles;
        colliderMesh.RecalculateNormals();
        chunkObject.meshCollider.convex = true;
    }

    public void UpdateUVs(ColorGenerator colorGenerator)
    {
        if (chunkObject == null || chunkObject.meshFilter == null)
            return;
        Vector2[] uv = new Vector2[filterResolution * filterResolution];
        int i = 0;
        for (int y = 0; y < filterResolution; y++)
        {
            for (int x = 0; x < filterResolution; x++)
            {
                Vector3 vertexPosition = GetPointOnCubeSphere(new Vector2(x, y) / (filterResolution - 1));
                // uv[i] = new Vector2(x, y);
                uv[i] = new Vector2(colorGenerator.BiomePercentFromPoint(vertexPosition), 0f);
                // uv[i] = Vector2.one;
                i++;
            }
        }
        chunkObject.meshFilter.sharedMesh.uv = uv;
    }

    private bool IsBoundsVisible(Camera camera, Vector3 centerPoint)
    {
        // Check if inside camera frustum
        Plane[] planes = new Plane[6];
        GeometryUtility.CalculateFrustumPlanes(camera, planes);

        if (!GeometryUtility.TestPlanesAABB(planes, chunkObject.meshRenderer.bounds))
            return false;
        
        // Horizon test
        Bounds chunkBounds = chunkObject.meshRenderer.bounds;
        Vector3 boundsMin = chunkBounds.min;
        Vector3 boundsMax = chunkBounds.max;

        Vector3[] testPoints = new Vector3[9];

        testPoints[0] = chunkBounds.center;
        testPoints[1] = boundsMin;
        testPoints[2] = boundsMax;
        testPoints[3] = new Vector3(boundsMin.x, boundsMax.y, boundsMin.z);
        testPoints[4] = new Vector3(boundsMin.x, boundsMax.y, boundsMax.z);
        testPoints[5] = new Vector3(boundsMax.x, boundsMin.y, boundsMin.z);
        testPoints[6] = new Vector3(boundsMax.x, boundsMin.y, boundsMax.z);
        testPoints[7] = new Vector3(boundsMax.x, boundsMax.y, boundsMin.z);
        testPoints[8] = new Vector3(boundsMin.x, boundsMin.y, boundsMax.z);
        Vector3 toCamera;
        Vector3 surfaceNormal;
        for (int i = 0; i < 8; i++)
        {
            toCamera = (camera.transform.position - testPoints[i]).normalized;
            surfaceNormal = (testPoints[i] - centerPoint).normalized;
            if (Vector3.Dot(surfaceNormal, toCamera) > 0f)
                return true;
        }
        return false;
    }

    private bool IsPointVisible(Camera camera, Vector3 renderPoint, Vector3 centerPoint)
    {
        // Check if inside camera frustum
        Vector3 viewport = camera.WorldToViewportPoint(renderPoint);

        if (viewport.z <= 0f ||
            viewport.x < 0f || viewport.x > 1f ||
            viewport.y < 0f || viewport.y > 1f)
            return false;

        // Horizon test
        Vector3 toCamera = (camera.transform.position - renderPoint).normalized;
        Vector3 surfaceNormal = (renderPoint - centerPoint).normalized;
        return Vector3.Dot(surfaceNormal, toCamera) > 0f;
    }

    public void RecursiveSetActive(bool active)
    {
        if (chunkObject != null && chunkObject.gameObject.activeSelf != active)
            chunkObject.gameObject.SetActive(active);
        if (children == null || children.Length == 0)
            return;
        foreach(TerrainChunk child in children)
        {
            if (child == null)
                continue;
            child.RecursiveSetActive(active);
        }
    }

    public void GenerateEmptyTree(Transform parent, ColorGenerator colorGenerator)
    {
        if (chunkObject == null)
        {
            GameObject meshGO = new GameObject("Mesh (" + detailLevel + " LOD)");
            meshGO.transform.SetParent(parent, false);
            chunkObject = meshGO.AddComponent<TerrainChunkObject>();
            chunkObject.Init(this);
            chunkObject.gameObject.layer = parent.gameObject.layer;
        }

        if (!chunkObject.gameObject.activeSelf)
            chunkObject.gameObject.SetActive(true);
        chunkObject.meshRenderer.sharedMaterial = colorGenerator.materialInstance;
        chunkObject.meshFilter.sharedMesh = new Mesh();
    }

    private void IncreaseLOD()
    {
        if (chunkObject != null && chunkObject.gameObject.activeSelf)
            chunkObject.gameObject.SetActive(false);
        
        if (children == null || children.Length == 0)
        {
            children = new TerrainChunk[4];
            float nextWidth = width * 0.5f;
            float nextSqrMaxScreenSize = sqrMaxScreenSize * 4f;
            Vector3 nextLocalRight = localRight * 0.5f;
            Vector3 nextLocalForward = localForward * 0.5f;
            children[0] = new TerrainChunk(
                shapeGenerator, 
                localPosition + nextLocalRight - nextLocalForward, 
                nextWidth, nextSqrMaxScreenSize, 
                filterResolution, colliderResolution, maxLOD, detailLevel + 1, 
                localUp, nextLocalRight, nextLocalForward); // Top left
            children[1] = new TerrainChunk(
                shapeGenerator, 
                localPosition + nextLocalRight + nextLocalForward, 
                nextWidth, nextSqrMaxScreenSize, 
                filterResolution, colliderResolution, maxLOD, detailLevel + 1, 
                localUp, nextLocalRight, nextLocalForward); // Top right
            children[2] = new TerrainChunk(
                shapeGenerator, 
                localPosition - nextLocalRight + nextLocalForward, 
                nextWidth, nextSqrMaxScreenSize, 
                filterResolution, colliderResolution, maxLOD, detailLevel + 1, 
                localUp, nextLocalRight, nextLocalForward); // Bottom right
            children[3] = new TerrainChunk(
                shapeGenerator, 
                localPosition - nextLocalRight - nextLocalForward, 
                nextWidth, nextSqrMaxScreenSize, 
                filterResolution, colliderResolution, maxLOD, detailLevel + 1, 
                localUp, nextLocalRight, nextLocalForward); // Bottom left
        }
    }

    public void GenerateTree(Vector3d realCamPos, ScaledTransform parent, ColorGenerator colorGenerator)
    {
        Vector3 worldRenderPos = parent.transform.TransformPoint(localPosition);
        double sqrDistance = (realCamPos - parent.TransformRenderPoint(worldRenderPos)).sqrMagnitude;
        double realWidth = width * parent.realRadius;

        if (detailLevel >= 0 && detailLevel < maxLOD && sqrDistance < 4.0 * realWidth * realWidth)
        {
            // Camera is close enough to this chunk which is also visible to the cam
            // so we need to increase the LOD so generate children with higher LOD in this chunk
            IncreaseLOD();

            foreach (TerrainChunk chunk in children)
            {
                chunk.GenerateTree(realCamPos, parent, colorGenerator);
            }
        }
        else
        {
            // Just use this chunk's LOD
            if (chunkObject == null)
            {
                GameObject meshGO = new GameObject("Mesh (" + detailLevel + " LOD)");
                meshGO.transform.SetParent(parent.transform, false);
                chunkObject = meshGO.AddComponent<TerrainChunkObject>();
                chunkObject.Init(this);
                chunkObject.gameObject.layer = parent.gameObject.layer;
            }

            if (!chunkObject.gameObject.activeSelf)
                chunkObject.gameObject.SetActive(true);
            chunkObject.meshRenderer.sharedMaterial = colorGenerator.materialInstance;
            chunkObject.meshFilter.sharedMesh = new Mesh();
            ConstructMesh();
            if (colliderResolution > 0)
                ConstructMeshCollider();
            UpdateUVs(colorGenerator);
        }
    }

    /// <summary>
    /// Generates/activates higher LOD child chunks depending on screen size of this body relative to camera, otherwise it just generates/activates this chunk
    /// </summary>
    /// <param name="sqrScreenSize"></param>
    /// <param name="parent"></param>
    /// <param name="colorGenerator"></param>
    /// <returns>Whether the chunk generated new chunks</returns>
    public bool UpdateTree(Vector3d realCamPos, ScaledTransform parent, ColorGenerator colorGenerator)
    {
        Vector3 worldRenderPos;
        bool isVisible;
        if (chunkObject != null)
        {
            worldRenderPos = chunkObject.meshRenderer.bounds.center;
            isVisible = IsBoundsVisible(Camera.main, parent.transform.position);
            // isVisible = IsMeshVisible(Camera.main, parent.transform, parent.transform.position, out worldRenderPos);
        }
        else
        {
            worldRenderPos = parent.transform.TransformPoint(localPosition);
            isVisible = IsPointVisible(Camera.main, worldRenderPos, parent.transform.position);
        }

        double sqrDistance = (realCamPos - parent.TransformRenderPoint(worldRenderPos)).sqrMagnitude;
        double realWidth = width * parent.realRadius;

        if (isVisible && detailLevel >= 0 && detailLevel < maxLOD && sqrDistance < 4.0 * realWidth * realWidth)
        {
            // Camera is close enough to this chunk which is also visible to the cam
            // so we need to increase the LOD so generate children with higher LOD in this chunk
            IncreaseLOD();

            bool generated = false;
            foreach (TerrainChunk chunk in children)
            {
                generated |= chunk.UpdateTree(realCamPos, parent, colorGenerator);
            }
            return generated;
        }
        else
        {
            // Just use this chunk's LOD
            if (chunkObject == null)
            {
                GameObject meshGO = new GameObject("Mesh (" + detailLevel + " LOD)");
                meshGO.transform.SetParent(parent.transform, false);
                chunkObject = meshGO.AddComponent<TerrainChunkObject>();
                chunkObject.Init(this);
                chunkObject.gameObject.layer = parent.gameObject.layer;
                chunkObject.meshFilter.sharedMesh = new Mesh();
                chunkObject.meshRenderer.sharedMaterial = colorGenerator.materialInstance;
                ConstructMesh();
                if (colliderResolution > 0)
                    ConstructMeshCollider();
                UpdateUVs(colorGenerator);
                return true;
            }
            if (chunkObject != null && !chunkObject.gameObject.activeSelf)
                chunkObject.gameObject.SetActive(true);

            if (children != null && children.Length > 0)
            {
                foreach (TerrainChunk chunk in children)
                {
                    chunk.RecursiveSetActive(false);
                }
            }
            return false;
        }
    }
}
