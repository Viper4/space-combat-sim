using SpaceStuff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDObject : MonoBehaviour
{
    private uint id;
    private HUDSystem _HUDSystem;

    [SerializeField] private RectTransform canvasRectangle;
    [SerializeField] private RectTransform bottomLeftCorner;
    [SerializeField] private RectTransform topLeftCorner;
    [SerializeField] private RectTransform topRightCorner;
    [SerializeField] private RectTransform bottomRightCorner;
    [SerializeField] private RectTransform centerOfMass;
    [SerializeField] private RectTransform predictedCenter;

    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField] private TextMeshProUGUI targetText;

    private Image bottomLeftImage;
    private Image topLeftImage;
    private Image topRightImage;
    private Image bottomRightImage;
    private Image centerOfMassImage;
    private Image predictedCenterImage;

    [SerializeField] private float killTime = 0.5f;
    private float killTimer;
    private float originalSize;

    public float sqrDistanceToCenter;

    private void Update()
    {
        killTimer -= Time.deltaTime;
        if (killTimer <= 0)
        {
            _HUDSystem.Remove(id);
            Destroy(gameObject);
        }
    }

    public void Init(HUDSystem _HUDSystem, Vector3 position, uint id, string details, bool detailsActive, Vector3 predictedPosition)
    {
        this._HUDSystem = _HUDSystem;
        this.id = id;
        _HUDSystem.Add(id, this);
        bottomLeftImage = bottomLeftCorner.GetChild(0).GetComponent<Image>();
        topLeftImage = topLeftCorner.GetChild(0).GetComponent<Image>();
        topRightImage = topRightCorner.GetChild(0).GetComponent<Image>();
        bottomRightImage = bottomRightCorner.GetChild(0).GetComponent<Image>();
        centerOfMassImage = centerOfMass.GetChild(0).GetComponent<Image>();
        predictedCenterImage = predictedCenter.GetChild(0).GetComponent<Image>();
        originalSize = bottomLeftCorner.sizeDelta.x;
        UpdateObject(position, details, detailsActive, predictedPosition);
    }

    public Color GetColor()
    {
        return predictedCenterImage.color;
    }

    public void SetColor(Color color)
    {
        color.a = 0.8f;
        bottomLeftImage.color = color;
        topLeftImage.color = color;
        topRightImage.color = color;
        bottomRightImage.color = color;
        centerOfMassImage.color = color;
        predictedCenterImage.color = color;
        detailsText.color = color;
        targetText.color = color;
    }

    public void UpdateObject(Vector3 position, string details, bool detailsActive, Vector3 predictedPosition)
    {
        if (!centerOfMass.gameObject.activeSelf)
        {
            centerOfMass.gameObject.SetActive(true);
        }

        if (bottomLeftCorner.gameObject.activeSelf)
        {
            bottomLeftCorner.gameObject.SetActive(false);
            topLeftCorner.gameObject.SetActive(false);
            topRightCorner.gameObject.SetActive(false);
            bottomRightCorner.gameObject.SetActive(false);
        }

        if (detailsActive)
        {
            if (!detailsText.gameObject.activeSelf)
                detailsText.gameObject.SetActive(true);
            if (detailsText != null)
            {
                detailsText.text = details;
            }
            if (!predictedCenter.gameObject.activeSelf)
                predictedCenter.gameObject.SetActive(true);
        }
        else
        {
            if (detailsText.gameObject.activeSelf)
                detailsText.gameObject.SetActive(false);
            if (predictedCenter.gameObject.activeSelf)
                predictedCenter.gameObject.SetActive(false);
        }

        killTimer = killTime;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up));

        centerOfMass.position = position;
        predictedCenter.position = predictedPosition;
    }

    public void UpdateObject(Vector3 position, Quadrilateral quad, string details, bool detailsActive, Vector3 predictedPosition)
    {
        if (SpaceGeometry.QuadrilateralIsZero(quad))
        {
            if (bottomLeftCorner.gameObject.activeSelf)
            {
                bottomLeftCorner.gameObject.SetActive(false);
                topLeftCorner.gameObject.SetActive(false);
                topRightCorner.gameObject.SetActive(false);
                bottomRightCorner.gameObject.SetActive(false);
                centerOfMass.gameObject.SetActive(false);
            }
            return;
        }

        if (!bottomLeftCorner.gameObject.activeSelf)
        {
            bottomLeftCorner.gameObject.SetActive(true);
            topLeftCorner.gameObject.SetActive(true);
            topRightCorner.gameObject.SetActive(true);
            bottomRightCorner.gameObject.SetActive(true);
            centerOfMass.gameObject.SetActive(true);
        }

        if (detailsActive)
        {
            if (!detailsText.gameObject.activeSelf)
                detailsText.gameObject.SetActive(true);
            if (detailsText != null)
            {
                detailsText.text = details;
            }
            if (!predictedCenter.gameObject.activeSelf)
                predictedCenter.gameObject.SetActive(true);
        }
        else
        {
            if (detailsText.gameObject.activeSelf)
                detailsText.gameObject.SetActive(false);
            if (predictedCenter.gameObject.activeSelf)
                predictedCenter.gameObject.SetActive(false);
        }

        killTimer = killTime;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up));

        bool bottomLeftVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p1, Camera.main, out Vector3 bottomLeftPos);
        bool topLeftVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p2, Camera.main, out Vector3 topLeftPos);
        bool topRightVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p3, Camera.main, out Vector3 topRightPos);
        bool bottomRightVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p4, Camera.main, out Vector3 bottomRightPos);

        if (!bottomLeftVisible && !topLeftVisible && !topRightVisible && !bottomRightVisible)
            return;

        bottomLeftCorner.position = bottomLeftPos;
        topLeftCorner.position = topLeftPos;
        topRightCorner.position = topRightPos;
        bottomRightCorner.position = bottomRightPos;
        centerOfMass.position = position;
        predictedCenter.position = predictedPosition;

        // If corners are too close together, scale them down
        float dx = Mathf.Abs(bottomLeftCorner.localPosition.x - bottomRightCorner.localPosition.x);
        float dy = Mathf.Abs(bottomLeftCorner.localPosition.y - topLeftCorner.localPosition.y);
        float minSize = Mathf.Min(originalSize, dx);
        minSize = Mathf.Min(minSize, dy);

        Vector2 newSizeVector = new Vector2(minSize, minSize);

        bottomLeftCorner.sizeDelta = newSizeVector;
        bottomRightCorner.sizeDelta = newSizeVector;
        topLeftCorner.sizeDelta = newSizeVector;
        topRightCorner.sizeDelta = newSizeVector;
    }

    public void SetTargetText(int turretsTargeting)
    {
        if (targetText == null)
            return;

        if (turretsTargeting <= 0)
        {
            targetText.text = "";
        }
        else
        {
            targetText.text = turretsTargeting.ToString();
        }
    }
}
