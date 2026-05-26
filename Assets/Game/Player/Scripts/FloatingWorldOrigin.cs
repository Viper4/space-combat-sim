using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceStuff;

public class FloatingWorldOrigin : MonoBehaviour
{
    public static FloatingWorldOrigin Instance { get; private set; }

    public Vector3d worldOriginPosition { get; private set; }
    public TransformChange cameraTC; // Use camera to update scaled space objects since camera can move slightly as child of origin

    [SerializeField] private float shiftThreshold = 500;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            ShiftOrigin();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void FixedUpdate()
    {
        if (transform.position.sqrMagnitude > shiftThreshold * shiftThreshold)
        {
            ShiftOrigin();
        }
    }

    private void ShiftOrigin()
    {
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (rootObject == gameObject || rootObject.CompareTag("IgnoreFloatingOrigin"))
                continue;

            if (rootObject.TryGetComponent<ScaledTransform>(out var rootScaledTransform))
            {
                if (!rootScaledTransform.inScaledSpace)
                {
                    rootObject.transform.position -= transform.position;
                }
            }
            else
            {
                rootObject.transform.position -= transform.position;
            }
        }
        // Define new origin position by adding current offset (transform.position) to world origin
        worldOriginPosition += transform.position.ToVector3d();
        //worldOrigin.SetPositionSilent(worldOrigin.realPosition + worldOrigin.transform.position.ToVector3d());
        transform.position = Vector3.zero;
        Debug.Log($"Shifted floating origin {transform.name} to {worldOriginPosition}");
    }
}
