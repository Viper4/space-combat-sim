using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDObject : MonoBehaviour
{
    private uint ID;
    private HUDSystem _HUDSystem;

    [SerializeField] private float borderSize = 100;
    [SerializeField] private RectTransform canvasRectangle;
    [SerializeField] private RectTransform leftBorder;
    [SerializeField] private RectTransform rightBorder;
    [SerializeField] private RectTransform topBorder;
    [SerializeField] private RectTransform bottomBorder;

    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI detailsText;
    [SerializeField] private TextMeshProUGUI targetText;

    [SerializeField] private float killTime = 0.5f;
    private float killTimer;

    private void Update()
    {
        killTimer -= Time.deltaTime;
        if (killTimer <= 0)
        {
            _HUDSystem.Remove(ID);
            Destroy(gameObject);
        }
    }

    private void OnValidate()
    {
        leftBorder.sizeDelta = new Vector2(borderSize, leftBorder.sizeDelta.y);
        rightBorder.sizeDelta = new Vector2(borderSize, rightBorder.sizeDelta.y);
        topBorder.sizeDelta = new Vector2(topBorder.sizeDelta.x, borderSize);
        bottomBorder.sizeDelta = new Vector2(bottomBorder.sizeDelta.x, borderSize);
    }

    public void Init(HUDSystem _HUDSystem, Vector3 position, Bounds bounds, uint ID, string name, string details)
    {
        this._HUDSystem = _HUDSystem;
        this.ID = ID;
        UpdateObject(position, bounds, name, details);
        _HUDSystem.Add(ID, this);
    }

    public Color GetColor()
    {
        return leftBorder.GetComponent<Image>().color;
    }

    public void SetColor(Color color)
    {
        leftBorder.GetComponent<Image>().color = color;
        rightBorder.GetComponent<Image>().color = color;
        topBorder.GetComponent<Image>().color = color;
        bottomBorder.GetComponent<Image>().color = color;
        nameText.color = color;
        detailsText.color = color;
        targetText.color = color;
    }

    public void UpdateObject(Vector3 position, Bounds bounds, string name, string details)
    {
        killTimer = killTime;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(transform.position - Camera.main.transform.position, Camera.main.transform.up));
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, Camera.main.transform.eulerAngles.z);

        // left, top, right, and bottom are Panels whose pivots are set as follows:
        // top: (1, 0)
        // right: (0, 0)
        // bottom: (0, 1)
        // left: (1, 1)

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] worldCorners = new Vector3[] {
            min,
            max,
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
        };

        Vector2 firstScreenPosition = Camera.main.WorldToScreenPoint(worldCorners[0]);
        float maxX = firstScreenPosition.x;
        float minX = firstScreenPosition.x;
        float maxY = firstScreenPosition.y;
        float minY = firstScreenPosition.y;
        for (int i = 1; i < 8; i++)
        {
            Vector2 screenPosition = Camera.main.WorldToScreenPoint(worldCorners[i]);
            if (screenPosition.x > maxX)
                maxX = screenPosition.x;
            if (screenPosition.x < minX)
                minX = screenPosition.x;
            if (screenPosition.y > maxY)
                maxY = screenPosition.y;
            if (screenPosition.y < minY)
                minY = screenPosition.y;
        }

        if(RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, new Vector2(maxX, maxY), Camera.main, out Vector3 topRight) && RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRectangle, new Vector2(minX, minY), Camera.main, out Vector3 bottomLeft))
        {
            leftBorder.gameObject.SetActive(true);
            rightBorder.gameObject.SetActive(true);
            topBorder.gameObject.SetActive(true);
            bottomBorder.gameObject.SetActive(true);

            topBorder.position = topRight;
            bottomBorder.position = bottomLeft;
            Vector3 localTopRight = topBorder.localPosition;
            Vector3 localBottomLeft = bottomBorder.localPosition;
            Vector3 localTopLeft = new Vector2(localBottomLeft.x, localTopRight.y);
            Vector3 localBottomRight = new Vector2(localTopRight.x, localBottomLeft.y);
            leftBorder.localPosition = localTopLeft;
            rightBorder.localPosition = localBottomRight;

            float width = Mathf.Abs(localTopRight.x - localTopLeft.x);
            float height = Mathf.Abs(localTopRight.y - localBottomRight.y);
            leftBorder.sizeDelta = new Vector2(borderSize, height + borderSize);
            rightBorder.sizeDelta = new Vector2(borderSize, height + borderSize);
            topBorder.sizeDelta = new Vector2(width + borderSize, borderSize);
            bottomBorder.sizeDelta = new Vector2(width + borderSize, borderSize);

            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                nameText.text = name;
                nameText.transform.position = topRight;
                nameText.transform.localPosition = new Vector3(nameText.transform.localPosition.x, nameText.transform.localPosition.y - borderSize, nameText.transform.localPosition.z);
            }

            if (detailsText != null)
            {
                detailsText.gameObject.SetActive(true);
                detailsText.text = details;
                detailsText.transform.position = topRight;
                detailsText.transform.localPosition = new Vector3(detailsText.transform.localPosition.x + borderSize, detailsText.transform.localPosition.y, detailsText.transform.localPosition.z);
            }

            if (targetText != null)
            {
                targetText.gameObject.SetActive(true);
                targetText.transform.position = topRight;
                detailsText.transform.localPosition = new Vector3(detailsText.transform.localPosition.x - borderSize, detailsText.transform.localPosition.y, detailsText.transform.localPosition.z);
            }
        }
        else
        {
            leftBorder.gameObject.SetActive(false);
            rightBorder.gameObject.SetActive(false);
            topBorder.gameObject.SetActive(false);
            bottomBorder.gameObject.SetActive(false);
            if (nameText != null)
                nameText.gameObject.SetActive(false);
            if (detailsText != null)
                detailsText.gameObject.SetActive(false);
            if (targetText != null)
                targetText.gameObject.SetActive(false);
        }
    }

    public void SetTargetText(string text)
    {
        if (targetText != null)
            targetText.text = text;
    }
}
