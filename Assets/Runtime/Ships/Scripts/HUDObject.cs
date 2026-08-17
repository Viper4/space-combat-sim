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
    [SerializeField] private LineRenderer predictedLineRenderer;

    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField] private TextMeshProUGUI targetText;

    private Camera _mainCamera;
    private Image _bottomLeftImage;
    private Image _topLeftImage;
    private Image _topRightImage;
    private Image _bottomRightImage;
    private Image _centerOfMassImage;
    private Image _predictedCenterImage;

    [SerializeField] private float killTime = 0.5f;
    private float _killTimer;
    private float _originalSize;

    public float sqrDistanceToCenter;

    private void Start()
    {
        
    }

    private void Update()
    {
        _killTimer -= Time.deltaTime;
        if (_killTimer <= 0)
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
        _bottomLeftImage = bottomLeftCorner.GetChild(0).GetComponent<Image>();
        _topLeftImage = topLeftCorner.GetChild(0).GetComponent<Image>();
        _topRightImage = topRightCorner.GetChild(0).GetComponent<Image>();
        _bottomRightImage = bottomRightCorner.GetChild(0).GetComponent<Image>();
        _centerOfMassImage = centerOfMass.GetChild(0).GetComponent<Image>();
        _predictedCenterImage = predictedCenter.GetChild(0).GetComponent<Image>();
        _originalSize = bottomLeftCorner.sizeDelta.x;
        UpdateObject(position, details, detailsActive, predictedPosition);
    }

    public Color GetColor()
    {
        return _predictedCenterImage.color;
    }

    public void SetColor(Color color)
    {
        color.a = 0.8f;
        _bottomLeftImage.color = color;
        _topLeftImage.color = color;
        _topRightImage.color = color;
        _bottomRightImage.color = color;
        _centerOfMassImage.color = color;
        _predictedCenterImage.color = color;
        predictedLineRenderer.startColor = color;
        predictedLineRenderer.endColor = color;
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

        _killTimer = killTime;
        Vector3 lookVector = transform.position - Camera.main.transform.position;
        if (lookVector.sqrMagnitude < 0.0001)
        {
            lookVector = Camera.main.transform.forward;
        }
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookVector, Camera.main.transform.up));

        centerOfMass.position = position;
        // predictedCenter.position = predictedPosition;
        predictedCenter.SetPositionAndRotation(predictedPosition, Quaternion.LookRotation(predictedPosition - Camera.main.transform.position, Camera.main.transform.up));
        predictedLineRenderer.SetPosition(0, position);
        predictedLineRenderer.SetPosition(1, predictedPosition);
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

        _killTimer = killTime;
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
        predictedCenter.SetPositionAndRotation(predictedPosition, Quaternion.LookRotation(predictedPosition - Camera.main.transform.position, Camera.main.transform.up));
        predictedLineRenderer.SetPosition(0, position);
        predictedLineRenderer.SetPosition(1, predictedPosition);

        // If corners are too close together, scale them down
        float dx = Mathf.Abs(bottomLeftCorner.localPosition.x - bottomRightCorner.localPosition.x);
        float dy = Mathf.Abs(bottomLeftCorner.localPosition.y - topLeftCorner.localPosition.y);
        float minSize = Mathf.Min(_originalSize, dx);
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
