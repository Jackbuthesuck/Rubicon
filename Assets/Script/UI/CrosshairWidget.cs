using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
public sealed partial class CrosshairWidget : MonoBehaviour
{
    [SerializeField] private Image[] pieces;
    [FormerlySerializedAs("idleColor")]
    [SerializeField] private Color defaultColor = Color.white;
    [FormerlySerializedAs("aimingColor")]
    [SerializeField] private Color aimAssistColor = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color unavailableColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField, Min(0f)] private float minimumGap = 18f;
    [SerializeField, Min(0f)] private float maximumGap = 44f;
    [SerializeField, Min(0f)] private float fadeStartSpeed = 2.2f;
    [SerializeField, Min(0f)] private float fadeEndSpeed = 3.5f;
    [SerializeField] private float movementRotation = 45f;
    [SerializeField, Min(0f)] private float recoilRecoverySpeed = 80f;
    [SerializeField, Min(0f)] private float recoilMultiplier = 2.5f;
    [SerializeField, Min(0f)] private float firingFadePerShot = 0.35f;
    [SerializeField, Min(0f)] private float firingFadeRecoverySpeed = 3.5f;
    [SerializeField, Min(0f)] private float chamberExpansionMultiplier = 2.5f;
    [SerializeField, Range(0f, 1f)] private float chamberFadeStrength = 0.95f;
    [SerializeField, Min(0f)] private float recoilAimPixelsPerDegree = 18f;
    [SerializeField] private WeaponCategory editorPreviewCategory = WeaponCategory.HG;
    [SerializeField] private bool showBasePiecesInEditor = true;
    [SerializeField, Range(0f, 80f)] private float editorPreviewRecoilGap;
    [SerializeField, Range(0f, 10f)] private float editorPreviewRunningSpeed;
    [SerializeField, Min(0.01f)] private float editorPreviewMaximumSpeed = 5f;
    [SerializeField] private CrosshairDefinition[] crosshairs = new CrosshairDefinition[0];

    private RectTransform[] pieceRects;
    private Vector2[] basePositions;
    private Vector2[] baseSizes;
    private Image[] defaultPieces;
    private CanvasGroup canvasGroup;
    private float baseGap;
    private float activeMaximumGap;
    private float currentRotation;
    private float recoilGap;
    private float firingFade;
    private RectTransform crosshairRect;
    private Vector2 baseCrosshairPosition;
    private Vector2 currentRecoilAimOffset;

    public void Initialize(Image[] crosshairPieces)
    {
        defaultPieces = crosshairPieces;
        EnsureGeneratedDefinitions();
        pieces = GetDefinitionPieces(WeaponCategory.HG);
        if (pieces == null || pieces.Length == 0)
        {
            pieces = defaultPieces;
        }

        CachePieceGeometry();
        crosshairRect = transform as RectTransform;
        baseCrosshairPosition = crosshairRect != null ? crosshairRect.anchoredPosition : Vector2.zero;
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

    public void AddRecoil(float recoil)
    {
        recoilGap = Mathf.Clamp(
            recoilGap + Mathf.Max(0f, recoil) * recoilMultiplier,
            0f,
            Mathf.Max(0f, activeMaximumGap > 0f ? activeMaximumGap : maximumGap));
        firingFade = Mathf.Clamp01(firingFade + Mathf.Max(0.01f, firingFadePerShot));
    }

    public float CurrentRotation => currentRotation;

    public void SetAimRecoilOffset(Vector2 recoilAngles)
    {
        if (crosshairRect == null)
        {
            crosshairRect = transform as RectTransform;
            baseCrosshairPosition = crosshairRect != null ? crosshairRect.anchoredPosition : Vector2.zero;
        }

        Vector2 targetOffset = new Vector2(recoilAngles.x, -recoilAngles.y) * recoilAimPixelsPerDegree;
        currentRecoilAimOffset = Vector2.Lerp(
            currentRecoilAimOffset,
            targetOffset,
            1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        if (crosshairRect != null)
        {
            crosshairRect.anchoredPosition = baseCrosshairPosition + currentRecoilAimOffset;
        }
    }

    public void SetAimState(bool isAiming, Vector3 localVelocity, float maximumSpeed, WeaponCategory category = WeaponCategory.HG, bool isUnavailable = false, bool isAimAssistActive = false, float chamberPressure = 0f, float lookHorizontal = 0f, float moveInputHorizontal = 0f)
    {
        CrosshairDefinition definition = GetDefinition(category);
        Image[] selectedPieces = definition != null && definition.pieces != null && definition.pieces.Length > 0
            ? definition.pieces
            : defaultPieces;
        if (selectedPieces != pieces)
        {
            pieces = selectedPieces;
            CachePieceGeometry();
        }

        if (pieces == null || pieceRects == null)
        {
            return;
        }

        Color color = isUnavailable
            ? definition != null ? definition.unavailableColor : unavailableColor
            : isAimAssistActive
            ? definition != null ? definition.aimAssistColor : aimAssistColor
            : definition != null ? definition.defaultColor : defaultColor;
        float minimum = definition != null ? definition.minimumGap : minimumGap;
        float maximum = definition != null ? definition.maximumGap : maximumGap;
        float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
        float movementAmount = Mathf.InverseLerp(0f, Mathf.Max(0.01f, maximumSpeed), speed);
        chamberPressure = Mathf.Clamp01(chamberPressure);
        recoilGap = Mathf.MoveTowards(recoilGap, 0f, recoilRecoverySpeed * Time.deltaTime);
        firingFade = Mathf.MoveTowards(firingFade, 0f, firingFadeRecoverySpeed * Time.deltaTime);
        float chamberGap = maximum * chamberPressure * chamberExpansionMultiplier;
        activeMaximumGap = maximum;
        float gap = Mathf.Min(
            maximum,
            Mathf.Lerp(minimum, maximum, movementAmount) + recoilGap + chamberGap);
        float expandScale = gap / Mathf.Max(0.01f, baseGap);
        float fadeStart = Mathf.Min(fadeStartSpeed, fadeEndSpeed);
        float fadeEnd = Mathf.Max(fadeStart + 0.01f, Mathf.Max(fadeStartSpeed, fadeEndSpeed));
        float fade = 1f - Mathf.InverseLerp(fadeStart, fadeEnd, speed);
        float chamberFade = 1f - chamberPressure * chamberFadeStrength;
        float movementRotationAmount = -moveInputHorizontal * movementRotation;
        float lookRotationAmount = -lookHorizontal * 45f;
        float targetRotation = Mathf.Clamp(movementRotationAmount + lookRotationAmount, -90f, 90f);
        currentRotation = Mathf.LerpAngle(currentRotation, targetRotation, 1f - Mathf.Exp(-12f * Time.deltaTime));

        canvasGroup.alpha = isAiming ? 1f : 0f;
        canvasGroup.interactable = isAiming;
        canvasGroup.blocksRaycasts = false;
        transform.localRotation = Quaternion.Euler(0f, 0f, currentRotation);

        for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
        {
            Image piece = pieces[pieceIndex];
            if (piece == null)
            {
                continue;
            }

            bool fadeMovement = ShouldFadeWhenMoving(definition, pieceIndex);
            bool fadeFiring = ShouldFadeWhenFiring(definition, pieceIndex);
            Color pieceColor = color;
            float movementFade = fadeMovement ? fade : 1f;
            float firingAlpha = fadeFiring ? 1f - firingFade : 1f;
            pieceColor.a *= isAiming ? movementFade * firingAlpha * chamberFade : 1f;
            piece.color = pieceColor;

            if (pieceIndex >= pieceRects.Length || pieceRects[pieceIndex] == null)
            {
                continue;
            }

            float scale = GetPieceScale(definition, pieceIndex, movementAmount, expandScale);
            pieceRects[pieceIndex].anchoredPosition = GetPiecePosition(definition, pieceIndex, movementAmount, scale);
            if (definition != null && definition.overridePieceSize)
            {
                bool horizontal = baseSizes[pieceIndex].x > baseSizes[pieceIndex].y;
                pieceRects[pieceIndex].sizeDelta = horizontal
                    ? new Vector2(definition.pieceLength, definition.pieceThickness)
                    : new Vector2(definition.pieceThickness, definition.pieceLength);
            }
            else if (definition != null && definition.pieceModes != null && pieceIndex < definition.pieceModes.Length && definition.pieceModes[pieceIndex] == CrosshairPieceMode.Contract)
            {
                float contractScale = Mathf.Lerp(1f, 0.35f, movementAmount);
                bool horizontal = baseSizes[pieceIndex].x > baseSizes[pieceIndex].y;
                pieceRects[pieceIndex].sizeDelta = horizontal
                    ? new Vector2(baseSizes[pieceIndex].x * contractScale, baseSizes[pieceIndex].y)
                    : new Vector2(baseSizes[pieceIndex].x, baseSizes[pieceIndex].y * contractScale);
            }
        }

        HideUnusedPieces(pieces);
    }

    private void CachePieceGeometry()
    {
        if (pieces == null)
        {
            return;
        }

        pieceRects = new RectTransform[pieces.Length];
        basePositions = new Vector2[pieces.Length];
        baseSizes = new Vector2[pieces.Length];
        baseGap = 0f;
        RectTransform parent = transform as RectTransform;
        for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
        {
            if (pieces[pieceIndex] == null)
            {
                continue;
            }

            RectTransform pieceRect = pieces[pieceIndex].rectTransform;
            pieceRects[pieceIndex] = pieceRect;
            basePositions[pieceIndex] = GetCenteredPosition(pieceRect, parent);
            baseSizes[pieceIndex] = pieceRect.sizeDelta;
            baseGap = Mathf.Max(baseGap, basePositions[pieceIndex].magnitude);
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.anchoredPosition = basePositions[pieceIndex];
        }
    }

    private CrosshairDefinition GetDefinition(WeaponCategory category)
    {
        if (crosshairs == null)
        {
            return null;
        }

        for (int index = 0; index < crosshairs.Length; index++)
        {
            if (crosshairs[index] != null && crosshairs[index].enabled && crosshairs[index].category == category)
            {
                return crosshairs[index];
            }
        }

        return null;
    }

    private Image[] GetDefinitionPieces(WeaponCategory category)
    {
        CrosshairDefinition definition = GetDefinition(category);
        return definition != null ? definition.pieces : null;
    }

    private static bool ShouldFadeWhenMoving(CrosshairDefinition definition, int pieceIndex)
    {
        if (definition != null && definition.fadeWhenMoving != null && pieceIndex < definition.fadeWhenMoving.Length)
        {
            return definition.fadeWhenMoving[pieceIndex];
        }

        return definition != null
            && definition.runningVisible != null
            && pieceIndex < definition.runningVisible.Length
            && !definition.runningVisible[pieceIndex];
    }

    private static bool ShouldFadeWhenFiring(CrosshairDefinition definition, int pieceIndex)
    {
        return definition != null
            && definition.fadeWhenFiring != null
            && pieceIndex < definition.fadeWhenFiring.Length
            && definition.fadeWhenFiring[pieceIndex];
    }

    private static float GetPieceScale(CrosshairDefinition definition, int pieceIndex, float movementAmount, float expandScale)
    {
        if (definition == null || definition.pieceModes == null || pieceIndex >= definition.pieceModes.Length)
        {
            return expandScale;
        }

        switch (definition.pieceModes[pieceIndex])
        {
            case CrosshairPieceMode.Contract:
                return Mathf.Lerp(1f, 0.35f, movementAmount);
            case CrosshairPieceMode.Stay:
                return 1f;
            default:
                return expandScale;
        }
    }

    private Vector2 GetPiecePosition(CrosshairDefinition definition, int pieceIndex, float movementAmount, float scale)
    {
        if (definition == null || definition.pieceModes == null || pieceIndex >= definition.pieceModes.Length || definition.pieceModes[pieceIndex] != CrosshairPieceMode.Contract)
        {
            return basePositions[pieceIndex] * scale;
        }

        float contractScale = Mathf.Lerp(1f, 0.35f, movementAmount);
        RectTransform pieceRect = pieceRects[pieceIndex];
        bool horizontal = baseSizes[pieceIndex].x > baseSizes[pieceIndex].y;
        Vector2 localAxis = horizontal ? Vector2.right : Vector2.up;
        Vector2 outerAxis = pieceRect.localRotation * localAxis;
        if (Vector2.Dot(outerAxis, basePositions[pieceIndex]) < 0f)
        {
            outerAxis = -outerAxis;
        }

        float baseLength = horizontal ? baseSizes[pieceIndex].x : baseSizes[pieceIndex].y;
        float movedLength = baseLength * contractScale;
        return basePositions[pieceIndex] + outerAxis * (baseLength - movedLength) * 0.5f;
    }

    private static Vector2 GetCenteredPosition(RectTransform piece, RectTransform parent)
    {
        Vector2 anchorOffset = Vector2.Scale(piece.anchorMin - parent.pivot, parent.rect.size);
        return piece.anchoredPosition + anchorOffset;
    }
}
