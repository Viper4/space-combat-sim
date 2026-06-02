using UnityEngine;
using SpaceStuff;

public class WorldSpaceOnly : MonoBehaviour
{
    [SerializeField] private float destroyThreshold = 4000f;

    private void Start()
    {
        FloatingWorldOrigin.Instance.OnOriginShift += OnOriginShift;
    }

    private void FixedUpdate()
    {
        if (transform.position.sqrMagnitude > destroyThreshold * destroyThreshold)
        {
            Destroy(gameObject);
        }
    }

    private void OnOriginShift(Vector3d shift)
    {
        Vector3 newPosition = transform.position - shift.ToVector3();
        if (newPosition.sqrMagnitude > destroyThreshold * destroyThreshold)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = newPosition;
    }

    private void OnDestroy()
    {
        FloatingWorldOrigin.Instance.OnOriginShift -= OnOriginShift;
    }
}
