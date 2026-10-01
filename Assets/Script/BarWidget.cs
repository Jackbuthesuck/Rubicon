using UnityEngine;
using UnityEngine.UI;

public sealed class BarWidget : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Text valueLabel;

    public void Initialize(Image fillImage, Text label)
    {
        fill = fillImage;
        valueLabel = label;
    }

    public void SetValue(float current, float maximum)
    {
        if (fill != null)
        {
            fill.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        }

        if (valueLabel != null)
        {
            valueLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}";
        }
    }

    public void SetVisuals(Color fillColor, Color backgroundColor)
    {
        if (fill != null)
        {
            fill.color = fillColor;
        }

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = backgroundColor;
        }
    }
}