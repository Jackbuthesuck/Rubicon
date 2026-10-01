using UnityEngine;
using UnityEngine.UI;

public sealed class CrosshairWidget : MonoBehaviour
{
    [SerializeField] private Image[] pieces;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color aimingColor = new Color(1f, 0.8f, 0.2f, 1f);

    public void Initialize(Image[] crosshairPieces)
    {
        pieces = crosshairPieces;
    }

    public void SetAiming(bool isAiming)
    {
        if (pieces == null)
        {
            return;
        }

        Color color = isAiming ? aimingColor : idleColor;
        foreach (Image piece in pieces)
        {
            if (piece != null)
            {
                piece.color = color;
            }
        }
    }
}