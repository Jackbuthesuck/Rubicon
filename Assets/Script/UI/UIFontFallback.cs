using UnityEngine;

public static class UIFontFallback
{
    public static Font Resolve(Font preferred)
    {
        if (preferred != null)
        {
            return preferred;
        }

        Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fallback == null)
        {
            fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return fallback;
    }

    public static void ApplyToHierarchy(Transform root, Font preferred)
    {
        if (root == null)
        {
            return;
        }

        Font font = Resolve(preferred);
        if (font == null)
        {
            return;
        }

        foreach (UnityEngine.UI.Text text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if (text.font == null)
            {
                text.font = font;
            }
        }
    }
}
