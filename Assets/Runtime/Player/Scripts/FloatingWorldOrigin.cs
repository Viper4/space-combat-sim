using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(ScaledTransform))]
public class FloatingWorldOrigin : MonoBehaviour
{
    public static FloatingWorldOrigin Instance { get; private set; }

    public Vector3d worldOriginPosition { get; private set; }
    public TransformChange cameraTC; // Use camera to update scaled space objects since camera can move slightly as child of origin

    [SerializeField] private float shiftThreshold = 1500;

    private ScaledTransform scaledTransform;

    public event Action<Vector3d> OnOriginShift;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            scaledTransform = GetComponent<ScaledTransform>();
            ShiftOrigin();
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

    public void ShiftOrigin()
    {
        Vector3d shift = scaledTransform.realPosition - worldOriginPosition;
        // Define new origin position by adding offset to world origin
        OnOriginShift?.Invoke(shift);
        worldOriginPosition += shift;
        transform.position = Vector3.zero;
        Debug.Log($"Shifted floating origin {transform.name} to {worldOriginPosition}");
    }
}
