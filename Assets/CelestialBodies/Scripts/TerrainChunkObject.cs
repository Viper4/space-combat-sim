using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainChunkObject : MonoBehaviour
{
    private TerrainChunk chunk;
    public MeshFilter meshFilter;
    public MeshCollider meshCollider;
    public MeshRenderer meshRenderer;
    private BoxCollider boxCollider;
    private int collidersInTrigger = 0;

    public void Init(TerrainChunk chunk)
    {
        this.chunk = chunk;
        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    // Box collider detects colliders entering the chunk and generates/activates the mesh collider
    public void UpdateBoxCollider()
    {
        if(boxCollider == null)
            boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.center = meshRenderer.bounds.center - transform.position;
        boxCollider.size = meshRenderer.bounds.size * 1.1f;
        boxCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collidersInTrigger == 0)
        {
            if(meshCollider != null)
            {
                meshCollider.enabled = true;
            }
            else
            {
                chunk.ConstructMeshCollider();
            }
        }
        collidersInTrigger++;
    }

    private void OnTriggerExit(Collider other)
    {
        collidersInTrigger--;
        if (collidersInTrigger < 0)
            collidersInTrigger = 0;
        if (collidersInTrigger == 0 && meshCollider != null)
        {
            meshCollider.enabled = false;
        }
    }
}
