using UnityEngine;

public static class ResponsiveIMGUI
{
    public const float ReferenceHeight = 720f;
    public const float MinimumScale = 0.75f;
    public const float MaximumScale = 2.5f;

    public static Matrix4x4 Begin(out float virtualWidth, out float virtualHeight)
    {
        Matrix4x4 previous = GUI.matrix;
        float scale = Mathf.Clamp(Screen.height / ReferenceHeight, MinimumScale, MaximumScale);
        virtualWidth = Screen.width / scale;
        virtualHeight = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        return previous;
    }

    public static void End(Matrix4x4 previous)
    {
        GUI.matrix = previous;
        GUI.color = Color.white;
    }

    public static Rect BottomLeft(float virtualWidth, float virtualHeight, float desiredWidth, float height)
    {
        float margin = Mathf.Clamp(virtualWidth * 0.025f, 12f, 24f);
        float width = Mathf.Min(desiredWidth, Mathf.Max(180f, virtualWidth - margin * 2f));
        return new Rect(margin, virtualHeight - height - margin, width, height);
    }

    public static Rect BottomCenter(float virtualWidth, float virtualHeight, float desiredWidth, float height)
    {
        float margin = Mathf.Clamp(virtualWidth * 0.025f, 12f, 24f);
        float width = Mathf.Min(desiredWidth, Mathf.Max(180f, virtualWidth - margin * 2f));
        return new Rect((virtualWidth - width) * 0.5f, virtualHeight - height - margin, width, height);
    }
}
