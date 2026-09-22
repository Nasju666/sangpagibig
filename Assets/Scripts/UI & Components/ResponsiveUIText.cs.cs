using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ResponsiveUIText : MonoBehaviour
{
    public enum AnchorPreset
    {
        TopCenter,
        BottomCenter,
        Center,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public AnchorPreset anchorPreset = AnchorPreset.Center;

    [Tooltip("Offset in pixels from the anchor")]
    public Vector2 offset;

    RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        Apply();
    }

    void OnValidate()
    {
        if (rect == null)
            rect = GetComponent<RectTransform>();

        Apply();
    }

    void Apply()
    {
        switch (anchorPreset)
        {
            case AnchorPreset.TopCenter:
                SetAnchor(new Vector2(0.5f, 1f));
                break;

            case AnchorPreset.BottomCenter:
                SetAnchor(new Vector2(0.5f, 0f));
                break;

            case AnchorPreset.Center:
                SetAnchor(new Vector2(0.5f, 0.5f));
                break;

            case AnchorPreset.TopLeft:
                SetAnchor(new Vector2(0f, 1f));
                break;

            case AnchorPreset.TopRight:
                SetAnchor(new Vector2(1f, 1f));
                break;

            case AnchorPreset.BottomLeft:
                SetAnchor(new Vector2(0f, 0f));
                break;

            case AnchorPreset.BottomRight:
                SetAnchor(new Vector2(1f, 0f));
                break;
        }

        rect.anchoredPosition = offset;
    }

    void SetAnchor(Vector2 anchor)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
    }
}
