using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunkObject : MonoBehaviour
{
    private TerrainChunk chunk;
    public MeshFilter meshFilter;
    public MeshCollider meshCollider;
    public MeshRenderer meshRenderer;

    public void Init(TerrainChunk chunk)
    {
        this.chunk = chunk;
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }
}
