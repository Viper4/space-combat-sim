using UnityEngine;
using SpaceStuff;
using Unity.Collections;

[RequireComponent(typeof(ScaledTransform))]
public class FloatingWorldOrigin : MonoBehaviour
{
    public static FloatingWorldOrigin Instance { get; private set; }

    public Vector3d worldOriginPosition { get; private set; }
    public TransformChange cameraTC; // Use camera to update scaled space objects since camera can move slightly as child of origin

    [SerializeField] private float shiftThreshold = 1500;

    private ScaledTransform scaledTransform;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            scaledTransform = GetComponent<ScaledTransform>();
            ShiftOrigin(transform.position.ToVector3d());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool OverShiftThreshold()
    {
        return (scaledTransform.realPosition - worldOriginPosition).sqrMagnitude > shiftThreshold * shiftThreshold;
    }

    public Vector3d GetOffset()
    {
        return scaledTransform.realPosition - worldOriginPosition;
    }

    public void ShiftOrigin(Vector3d shift)
    {
        // Define new origin position by adding offset to world origin
        worldOriginPosition += shift;
        transform.position = Vector3.zero;
        Debug.Log($"Shifted floating origin {transform.name} to {worldOriginPosition}");
    }
}
