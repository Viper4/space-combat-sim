using SpaceStuff;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDObject : MonoBehaviour
{
    private uint ID;
    private HUDSystem _HUDSystem;

    [SerializeField] private RectTransform canvasRectangle;
    [SerializeField] private RectTransform bottomLeftCorner;
    [SerializeField] private RectTransform topLeftCorner;
    [SerializeField] private RectTransform topRightCorner;
    [SerializeField] private RectTransform bottomRightCorner;
    [SerializeField] private RectTransform boundsCenter;
    [SerializeField] private RectTransform centerOfMass;

    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField] private TextMeshProUGUI targetText;

    private Image bottomLeftImage;
    private Image topLeftImage;
    private Image topRightImage;
    private Image bottomRightImage;
    private Image boundsCenterImage;
    private Image centerOfMassImage;

    [SerializeField] private float killTime = 0.5f;
    private float killTimer;

    private float originalSize;

    private void Update()
    {
        killTimer -= Time.deltaTime;
        if (killTimer <= 0)
        {
            _HUDSystem.Remove(ID);
            Destroy(gameObject);
        }
    }

    public void Init(HUDSystem _HUDSystem, Vector3 position, uint ID, string details)
    {
        this._HUDSystem = _HUDSystem;
        this.ID = ID;
        _HUDSystem.Add(ID, this);
        bottomLeftImage = bottomLeftCorner.GetChild(0).GetComponent<Image>();
        topLeftImage = topLeftCorner.GetChild(0).GetComponent<Image>();
        topRightImage = topRightCorner.GetChild(0).GetComponent<Image>();
        bottomRightImage = bottomRightCorner.GetChild(0).GetComponent<Image>();
        boundsCenterImage = boundsCenter.GetChild(0).GetComponent<Image>();
        centerOfMassImage = centerOfMass.GetChild(0).GetComponent<Image>();
        originalSize = bottomLeftCorner.sizeDelta.x;
        UpdateObject(position, details);
    }

    public void Init(HUDSystem _HUDSystem, Vector3 position, Quadrilateral quad, uint ID, string details)
    {
        this._HUDSystem = _HUDSystem;
        this.ID = ID;
        _HUDSystem.Add(ID, this);
        bottomLeftImage = bottomLeftCorner.GetChild(0).GetComponent<Image>();
        topLeftImage = topLeftCorner.GetChild(0).GetComponent<Image>();
        topRightImage = topRightCorner.GetChild(0).GetComponent<Image>();
        bottomRightImage = bottomRightCorner.GetChild(0).GetComponent<Image>();
        boundsCenterImage = boundsCenter.GetChild(0).GetComponent<Image>();
        centerOfMassImage = centerOfMass.GetChild(0).GetComponent<Image>();
        originalSize = bottomLeftCorner.sizeDelta.x;
        UpdateObject(position, quad, details);
    }

    public Color GetColor()
    {
        return boundsCenterImage.color;
    }

    public void SetColor(Color color)
    {
        color.a = 0.8f;
        bottomLeftImage.color = color;
        topLeftImage.color = color;
        topRightImage.color = color;
        bottomRightImage.color = color;
        boundsCenterImage.color = color;
        centerOfMassImage.color = color;
        detailsText.color = color;
        targetText.color = color;
    }

    public void UpdateObject(Vector3 position, string details)
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
            boundsCenter.gameObject.SetActive(false);
        }

        if (detailsText != null)
        {
            detailsText.text = details;
        }

        killTimer = killTime;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up));

        centerOfMass.position = position;
    }

    public void UpdateObject(Vector3 position, Quadrilateral quad, string details)
    {
        if (SpaceGeometry.QuadrilateralIsZero(quad))
        {
            if (bottomLeftCorner.gameObject.activeSelf)
            {
                bottomLeftCorner.gameObject.SetActive(false);
                topLeftCorner.gameObject.SetActive(false);
                topRightCorner.gameObject.SetActive(false);
                bottomRightCorner.gameObject.SetActive(false);
                boundsCenter.gameObject.SetActive(false);
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
            boundsCenter.gameObject.SetActive(true);
            centerOfMass.gameObject.SetActive(true);
        }

        if (detailsText != null)
        {
            detailsText.text = details;
        }

        killTimer = killTime;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up));

        Vector2 centerScreenPoint = (quad.p1 + quad.p2 + quad.p3 + quad.p4) / 4;

        bool bottomLeftVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p1, Camera.main, out Vector3 bottomLeftPos);
        bool topLeftVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p2, Camera.main, out Vector3 topLeftPos);
        bool topRightVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p3, Camera.main, out Vector3 topRightPos);
        bool bottomRightVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, quad.p4, Camera.main, out Vector3 bottomRightPos);
        bool centerVisible = RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, centerScreenPoint, Camera.main, out Vector3 centerPos);

        if (!bottomLeftVisible && !topLeftVisible && !topRightVisible && !bottomRightVisible && !centerVisible)
            return;

        bottomLeftCorner.position = bottomLeftPos;
        topLeftCorner.position = topLeftPos;
        topRightCorner.position = topRightPos;
        bottomRightCorner.position = bottomRightPos;
        boundsCenter.position = centerPos;
        centerOfMass.position = position;

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

    public void SetTargetText(string text)
    {
        if (targetText != null)
            targetText.text = text;
    }
}
