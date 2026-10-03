using UnityEngine;
using UnityEngine.UI;

public sealed class BarWidget : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Text valueLabel;
    private int lastDisplayedCurrent = int.MinValue;
    private int lastDisplayedMaximum = int.MinValue;

    public void Initialize(Image fillImage, Text label)
    {
        fill = fillImage;
        valueLabel = label;
        lastDisplayedCurrent = int.MinValue;
        lastDisplayedMaximum = int.MinValue;
    }

    public void SetValue(float current, float maximum)
    {
        if (fill != null)
        {
            fill.fillAmount = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        }

        int displayedCurrent = Mathf.CeilToInt(current);
        int displayedMaximum = Mathf.CeilToInt(maximum);
        if (valueLabel != null
            && (displayedCurrent != lastDisplayedCurrent || displayedMaximum != lastDisplayedMaximum))
        {
            valueLabel.text = $"{displayedCurrent} / {displayedMaximum}";
            lastDisplayedCurrent = displayedCurrent;
            lastDisplayedMaximum = displayedMaximum;
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