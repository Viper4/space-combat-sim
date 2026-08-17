using UnityEngine;

public class PreviewRendering : MonoBehaviour
{
    [SerializeField] private Transform start;
    [SerializeField] private Transform end;
    [SerializeField] private float transformLerpSpeed = 1f;
    [SerializeField] private bool lerpTransforms = true;

    [SerializeField] private float speedOfLight = 1f;
    [SerializeField] private float startSpeed = -1f;
    [SerializeField] private float endSpeed = 1f;
    [SerializeField] private float velocityLerpSpeed = 0.5f;
    [SerializeField] private bool changeSkyboxMaterial;

    [Tooltip("Seconds spent easing position direction when the velocity sign flips.")]
    [SerializeField] private float directionTransitionTime = 1f;

    // Position along the start->end path, wrapping in [0, 1).
    private float transformT;
    // -1..1 signed speed multiplier for position movement; smoothly eased
    // whenever the velocity's sign changes.
    private float currentDirection = 1f;
    private float directionVelocity;

    // Drives the smoothly oscillating relative-velocity value.
    private float velocityPhase;
    private float previousVelocitySign = 1f;

    private Transform[] children;
    private Material[] childMaterials;
    private Vector3 velocityDir;

    private Material _skyboxMaterial;

    private void Start()
    {
        children = new Transform[transform.childCount];
        childMaterials = new Material[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            children[i] = transform.GetChild(i);
            childMaterials[i] = children[i].GetComponent<MeshRenderer>().material;
            childMaterials[i].SetFloat(ScaledSpaceVisuals.SpeedOfLightID, speedOfLight);
        }
        velocityDir = (end.position - start.position).normalized;
        _skyboxMaterial = Instantiate(RenderSettings.skybox);
        RenderSettings.skybox = _skyboxMaterial;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Destroy(childMaterials[i]);
        }
    }

    private void Update()
    {
        velocityPhase += Time.deltaTime * velocityLerpSpeed;
        float velocityT = (Mathf.Sin(velocityPhase) + 1f) * 0.5f;

        // Signed speed the velocity is currently moving at (matches the
        // Lerp used for the material vector below): negative while easing
        // toward startSpeed, positive while easing toward endSpeed.
        float signedVelocity = Mathf.Lerp(startSpeed, endSpeed, velocityT);
        float velocitySign = signedVelocity >= 0f ? 1f : -1f;

        // Whenever the velocity's sign flips, retarget the position
        // direction to match it. SmoothDamp eases the change instead of
        // snapping, so it doesn't look abrupt even on an instant flip.
        if (velocitySign != previousVelocitySign)
        {
            previousVelocitySign = velocitySign;
        }

        if (lerpTransforms)
        {
            currentDirection = Mathf.SmoothDamp(currentDirection, velocitySign, ref directionVelocity, directionTransitionTime);
            transformT += Time.deltaTime * transformLerpSpeed * currentDirection; // flip current direction since its relativeVelocity
            // Wrap into [0, 1) rather than clamping/bouncing.
            transformT = Mathf.Repeat(transformT, 1f);
        }

        int childCount = children.Length;
        for (int i = 0; i < childCount; i++)
        {
            // Spread children evenly around the loop and let each wrap
            // independently once it reaches the end of the path.
            float phaseOffset = childCount > 0 ? (i / (float)childCount) : 0f;
            float childT = Mathf.Repeat(transformT + phaseOffset, 1f);

            children[i].SetPositionAndRotation(
                Vector3.Lerp(start.position, end.position, childT),
                Quaternion.Slerp(start.rotation, end.rotation, childT));

            childMaterials[i].SetVector(ScaledSpaceVisuals.ObserverVelocityID, velocityDir * signedVelocity);
        }
        if (changeSkyboxMaterial)
        {
            _skyboxMaterial.SetFloat(ScaledSpaceVisuals.SpeedOfLightID, speedOfLight);
            _skyboxMaterial.SetVector(ScaledSpaceVisuals.ObserverVelocityID, velocityDir * signedVelocity);
        }
    }
}