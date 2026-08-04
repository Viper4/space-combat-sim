using UnityEngine;

[ExecuteAlways]
public class TransformLayoutGroup : MonoBehaviour
{
    private enum StartPoint
    {
        Center,
        Left,
        Right,
        Bottom,
        Top,
        TopLeft,
        BottomLeft,
        TopRight,
        BottomRight
    }

    [SerializeField] private StartPoint startPoint = StartPoint.Center;

    private enum LayoutAxis
    {
        X,
        Y,
        Z
    }

    [SerializeField] private LayoutAxis layoutAxis = LayoutAxis.X;
    [SerializeField] private float spacing = 0.5f;

    [SerializeField] private Transform label;

    private void OnValidate()
    {
        UpdateLayout();
    }

    [ContextMenu("Update Layout")]
    public void UpdateLayout()
    {
        int childCount = transform.childCount;

        if (childCount == 0)
            return;

        float totalLength = (childCount - 1) * spacing;

        float startOffset = startPoint switch
        {
            StartPoint.Center => -totalLength * 0.5f,

            StartPoint.Left => 0f,
            StartPoint.Bottom => 0f,
            StartPoint.BottomLeft => 0f,

            StartPoint.Right => -totalLength,
            StartPoint.Top => -totalLength,
            StartPoint.TopRight => -totalLength,

            StartPoint.TopLeft => 0f,
            StartPoint.BottomRight => -totalLength,

            _ => -totalLength * 0.5f
        };

        Vector3 midpoint = Vector3.zero;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Vector3 localPos = Vector3.zero;

            float offset = startOffset + i * spacing;

            switch (layoutAxis)
            {
                case LayoutAxis.X:
                    localPos.x = offset;
                    break;
                case LayoutAxis.Y:
                    localPos.y = offset;
                    break;
                case LayoutAxis.Z:
                    localPos.z = offset;
                    break;
            }

            child.localPosition = localPos;
            if (i == 0)
            {
                midpoint += localPos;
            }
            if (i == childCount - 1)
            {
                midpoint += localPos;
                midpoint /= 2f;
            }
        }

        if (label != null)
        {
            label.localPosition = midpoint;
        }
    }
}