using UnityEngine;
using UnityEngine.UI;

public sealed class CrosshairWidget : MonoBehaviour
{
    [SerializeField] private Image[] pieces;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color aimingColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField, Min(0f)] private float minimumGap = 18f;
    [SerializeField, Min(0f)] private float maximumGap = 44f;
    [SerializeField, Min(0f)] private float fadeStartSpeed = 2.2f;
    [SerializeField, Min(0f)] private float fadeEndSpeed = 3.5f;
    [SerializeField] private float movementRotation = 7f;

    private RectTransform[] pieceRects;
    private Vector2[] basePositions;
    private CanvasGroup canvasGroup;
    private float currentRotation;

    public void Initialize(Image[] crosshairPieces)
    {
        pieces = crosshairPieces;
        pieceRects = new RectTransform[pieces.Length];
        basePositions = new Vector2[pieces.Length];
        for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
        {
            if (pieces[pieceIndex] == null)
            {
                continue;
            }

            pieceRects[pieceIndex] = pieces[pieceIndex].rectTransform;
            basePositions[pieceIndex] = pieceRects[pieceIndex].anchoredPosition;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        SetAimState(false, Vector3.zero, 1f);
    }

    public void SetAiming(bool isAiming)
    {
        SetAimState(isAiming, Vector3.zero, 1f);
    }

    public void SetAimState(bool isAiming, Vector3 localVelocity, float maximumSpeed)
    {
        if (pieces == null)
        {
            return;
        }

        Color color = isAiming ? aimingColor : idleColor;
        float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
        float movementAmount = Mathf.InverseLerp(0f, Mathf.Max(0.01f, maximumSpeed), speed);
        float gap = Mathf.Lerp(minimumGap, maximumGap, movementAmount);
        float gapScale = gap / Mathf.Max(0.01f, minimumGap);
        float visibility = 1f - Mathf.InverseLerp(fadeStartSpeed, Mathf.Max(fadeStartSpeed + 0.01f, fadeEndSpeed), speed);
        float targetRotation = Mathf.Clamp(-localVelocity.x * movementRotation, -35f, 35f);
        currentRotation = Mathf.LerpAngle(currentRotation, targetRotation, 1f - Mathf.Exp(-12f * Time.deltaTime));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = isAiming ? visibility : 0f;
            canvasGroup.interactable = isAiming;
            canvasGroup.blocksRaycasts = false;
        }

        transform.localRotation = Quaternion.Euler(0f, 0f, currentRotation);
        foreach (Image piece in pieces)
        {
            if (piece != null)
            {
                piece.color = color;
            }
        }

        for (int pieceIndex = 0; pieceIndex < pieceRects.Length; pieceIndex++)
        {
            if (pieceRects[pieceIndex] != null)
            {
                pieceRects[pieceIndex].anchoredPosition = basePositions[pieceIndex] * gapScale;
            }
        }
    }
}