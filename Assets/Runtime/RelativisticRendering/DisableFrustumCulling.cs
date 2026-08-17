using UnityEngine;

public class DisableFrustumCulling : MonoBehaviour
{
    private void Start()
    {
        // For regular meshes
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            // Set the local bounds to an extremely large volume
            meshFilter.sharedMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        }

        // For skinned meshes (characters, animated objects)
        SkinnedMeshRenderer skinnedMesh = GetComponent<SkinnedMeshRenderer>();
        if (skinnedMesh != null)
        {
            // SkinnedMeshRenderer uses localBounds directly
            skinnedMesh.localBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
        }
    }
}