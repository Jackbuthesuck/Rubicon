using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[System.Serializable]
public sealed class CrosshairPieceDefinition
{
    public Vector2 position;
    public Vector2 size = new Vector2(8f, 8f);
    public float rotation;
}

[System.Serializable]
public sealed class CrosshairDefinition
{
    public bool enabled = true;
    public WeaponCategory category;
    public Image[] pieces = new Image[0];
    public CrosshairPieceDefinition[] layoutPieces = new CrosshairPieceDefinition[0];
    public CrosshairPieceMode[] pieceModes = new CrosshairPieceMode[0];
    public bool[] runningVisible = new bool[0];
    public bool[] fadeWhenMoving = new bool[0];
    public bool[] fadeWhenFiring = new bool[0];
    [Min(0f)] public float minimumGap = 18f;
    [Min(0f)] public float maximumGap = 44f;
    [Min(0f)] public float pieceLength = 16f;
    [Min(0f)] public float pieceThickness = 4f;
    public bool overridePieceSize = true;
    [FormerlySerializedAs("idleColor")] public Color defaultColor = Color.white;
    [FormerlySerializedAs("aimingColor")] public Color aimAssistColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Color unavailableColor = new Color(0.55f, 0.55f, 0.55f, 1f);
}

public enum CrosshairPieceMode
{
    Expand,
    Contract,
    Stay
}

[ExecuteAlways]
public sealed class CrosshairWidget : MonoBehaviour
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
    private float currentRotation;
    private float recoilGap;
    private float firingFade;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            return;
        }

        defaultPieces = FindFallbackPieces();
        EnsureGeneratedDefinitions();
        UpdateEditorPreview();
        ApplyEditorPreview();
        MarkSceneDirty();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        defaultPieces = FindFallbackPieces();
        EnsureGeneratedDefinitions();
        UpdateEditorPreview();
        ApplyEditorPreview();
        MarkSceneDirty();
    }

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
        recoilGap = Mathf.Clamp(recoilGap + Mathf.Max(0f, recoil) * recoilMultiplier, 0f, 80f);
        firingFade = Mathf.Clamp01(firingFade + Mathf.Max(0.01f, firingFadePerShot));
    }

    public float CurrentRotation => currentRotation;

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
        float gap = Mathf.Lerp(minimum, maximum, movementAmount) + recoilGap + chamberGap;
        float expandScale = gap / Mathf.Max(0.01f, baseGap);
        float fade = 1f - Mathf.InverseLerp(fadeStartSpeed, Mathf.Max(fadeStartSpeed + 0.01f, fadeEndSpeed), speed);
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

    private void EnsureGeneratedDefinitions()
    {
        if (crosshairs == null || crosshairs.Length == 0)
        {
            return;
        }

        ConfigureGeneratedDefinition(WeaponCategory.HG, new[]
        {
            new PieceSpec(new Vector2(0f, 34f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-34f, 0f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(34f, 0f), new Vector2(8f, 8f), 0f)
        }, new[] { CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand }, new[] { true, true, true }, 30f, 62f);

        ConfigureGeneratedDefinition(WeaponCategory.SG, new[]
        {
            new PieceSpec(new Vector2(0f, 48f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(34f, 34f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(48f, 0f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(34f, -34f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(0f, -48f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-34f, -34f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-48f, 0f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-34f, 34f), new Vector2(8f, 8f), 0f),
            new PieceSpec(Vector2.zero, new Vector2(8f, 8f), 0f)
        }, new[] { CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Stay }, new[] { false, false, true, false, false, false, true, false, false }, 38f, 96f);

        ConfigureGeneratedDefinition(WeaponCategory.SR, new[]
        {
            new PieceSpec(new Vector2(0f, 42f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(0f, -42f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-42f, 0f), new Vector2(30f, 4f), 0f),
            new PieceSpec(new Vector2(42f, 0f), new Vector2(30f, 4f), 0f)
        }, new[] { CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand }, new[] { true, true, true, true }, 44f, 128f);

        ConfigureGeneratedDefinition(WeaponCategory.AR, new[]
        {
            new PieceSpec(new Vector2(0f, 42f), new Vector2(10f, 10f), 0f),
            new PieceSpec(new Vector2(0f, -42f), new Vector2(10f, 10f), 0f),
            new PieceSpec(new Vector2(-42f, 0f), new Vector2(10f, 10f), 0f),
            new PieceSpec(new Vector2(42f, 0f), new Vector2(10f, 10f), 0f)
        }, new[] { CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand }, new[] { true, true, true, true }, 42f, 150f);

        ConfigureGeneratedDefinition(WeaponCategory.SMG, new[]
        {
            new PieceSpec(new Vector2(-18f, 18f), new Vector2(38f, 4f), -45f),
            new PieceSpec(new Vector2(18f, 18f), new Vector2(38f, 4f), 45f),
            new PieceSpec(new Vector2(0f, -22f), new Vector2(4f, 42f), 0f),
            new PieceSpec(new Vector2(-12f, -12f), new Vector2(24f, 3f), 45f),
            new PieceSpec(new Vector2(12f, -12f), new Vector2(24f, 3f), -45f),
            new PieceSpec(new Vector2(0f, 14f), new Vector2(3f, 28f), 0f)
        }, new[] { CrosshairPieceMode.Contract, CrosshairPieceMode.Contract, CrosshairPieceMode.Contract, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand }, new[] { false, false, false, true, true, true }, 32f, 74f);

        ConfigureGeneratedDefinition(WeaponCategory.LMG, new[]
        {
            new PieceSpec(new Vector2(-44f, 34f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-22f, 12f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(0f, -12f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(22f, 12f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(44f, 34f), new Vector2(8f, 8f), 0f)
        }, new[] { CrosshairPieceMode.Expand, CrosshairPieceMode.Stay, CrosshairPieceMode.Stay, CrosshairPieceMode.Stay, CrosshairPieceMode.Expand }, new[] { true, true, true, true, true }, 34f, 66f);
    }

    private Image[] FindFallbackPieces()
    {
        Image[] fallback = new Image[4];
        for (int pieceIndex = 0; pieceIndex < fallback.Length; pieceIndex++)
        {
            Transform piece = transform.Find($"Piece{pieceIndex + 1}");
            fallback[pieceIndex] = piece != null ? piece.GetComponent<Image>() : null;
        }

        return fallback;
    }

    private void UpdateEditorPreview()
    {
        if (defaultPieces != null)
        {
            for (int pieceIndex = 0; pieceIndex < defaultPieces.Length; pieceIndex++)
            {
                SetPieceAlpha(defaultPieces[pieceIndex], showBasePiecesInEditor ? 1f : 0f);
            }
        }

        if (crosshairs == null)
        {
            return;
        }

        CrosshairDefinition previewDefinition = GetDefinition(editorPreviewCategory);
        for (int definitionIndex = 0; definitionIndex < crosshairs.Length; definitionIndex++)
        {
            Image[] generatedPieces = crosshairs[definitionIndex]?.pieces;
            if (generatedPieces == null)
            {
                continue;
            }

            for (int pieceIndex = 0; pieceIndex < generatedPieces.Length; pieceIndex++)
            {
                SetPieceAlpha(generatedPieces[pieceIndex], crosshairs[definitionIndex] == previewDefinition ? 1f : 0f);
            }
        }
    }

    private void ApplyEditorPreview()
    {
        CrosshairDefinition definition = GetDefinition(editorPreviewCategory);
        Image[] previewPieces = definition != null ? definition.pieces : defaultPieces;
        if (previewPieces == null || previewPieces.Length == 0)
        {
            return;
        }

        if (pieces != previewPieces || pieceRects == null || pieceRects.Length != previewPieces.Length)
        {
            pieces = previewPieces;
            CachePieceGeometry();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        recoilGap = editorPreviewRecoilGap;
        firingFade = Mathf.Clamp01(editorPreviewRecoilGap / 80f);
        SetAimState(
            true,
            new Vector3(0f, 0f, editorPreviewRunningSpeed),
            editorPreviewMaximumSpeed,
            editorPreviewCategory);
    }

    private static void SetPieceAlpha(Image piece, float alpha)
    {
        if (piece == null)
        {
            return;
        }

        Color color = piece.color;
        color.a = alpha;
        piece.color = color;
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().IsValid())
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private bool UsesSharedFallbackPieces()
    {
        if (defaultPieces == null || defaultPieces.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < crosshairs.Length; index++)
        {
            if (crosshairs[index] != null && crosshairs[index].pieces != null && crosshairs[index].pieces.Length > 0 && !HasSamePieces(crosshairs[index].pieces, defaultPieces))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSamePieces(Image[] first, Image[] second)
    {
        if (first == null || second == null || first.Length != second.Length)
        {
            return false;
        }

        for (int index = 0; index < first.Length; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    private void ConfigureGeneratedDefinition(WeaponCategory category, PieceSpec[] defaultLayout, CrosshairPieceMode[] modes, bool[] runningVisible, float minimum, float maximum)
    {
        CrosshairDefinition definition = GetDefinition(category);
        if (definition == null)
        {
            return;
        }

        bool hasLayout = definition.layoutPieces != null && definition.layoutPieces.Length > 0;
        if (definition.layoutPieces == null || definition.layoutPieces.Length == 0)
        {
            definition.layoutPieces = ConvertLayout(defaultLayout);
        }

        if (definition.pieces == null || definition.pieces.Length == 0)
        {
            definition.pieces = CreatePieces(category.ToString(), definition.layoutPieces);
        }

        if (!hasLayout || definition.pieceModes == null || definition.pieceModes.Length != modes.Length)
        {
            definition.pieceModes = modes;
        }

        if (!hasLayout || definition.runningVisible == null || definition.runningVisible.Length != runningVisible.Length)
        {
            definition.runningVisible = runningVisible;
        }

        if (definition.fadeWhenMoving == null || definition.fadeWhenMoving.Length != runningVisible.Length)
        {
            definition.fadeWhenMoving = new bool[runningVisible.Length];
            for (int index = 0; index < runningVisible.Length; index++)
            {
                definition.fadeWhenMoving[index] = !runningVisible[index];
            }
        }

        if (definition.fadeWhenFiring == null || definition.fadeWhenFiring.Length != runningVisible.Length)
        {
            definition.fadeWhenFiring = new bool[runningVisible.Length];
            for (int index = 0; index < definition.fadeWhenFiring.Length; index++)
            {
                definition.fadeWhenFiring[index] = true;
            }
        }

        if (!hasLayout)
        {
            definition.minimumGap = minimum;
            definition.maximumGap = maximum;
            definition.pieceLength = 8f;
            definition.pieceThickness = 8f;
            definition.overridePieceSize = false;
        }
    }

    private static CrosshairPieceDefinition[] ConvertLayout(PieceSpec[] specs)
    {
        CrosshairPieceDefinition[] layout = new CrosshairPieceDefinition[specs.Length];
        for (int index = 0; index < specs.Length; index++)
        {
            layout[index] = new CrosshairPieceDefinition
            {
                position = specs[index].position,
                size = specs[index].size,
                rotation = specs[index].rotation
            };
        }

        return layout;
    }

    private Image[] CreatePieces(string groupName, CrosshairPieceDefinition[] specs)
    {
        Image[] generatedPieces = new Image[specs.Length];
        for (int index = 0; index < specs.Length; index++)
        {
            GameObject pieceObject = new GameObject($"{groupName}Piece{index + 1}", typeof(RectTransform), typeof(Image));
            pieceObject.transform.SetParent(transform, false);
            RectTransform pieceRect = pieceObject.GetComponent<RectTransform>();
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.anchoredPosition = specs[index].position;
            pieceRect.sizeDelta = specs[index].size;
            pieceRect.localRotation = Quaternion.Euler(0f, 0f, specs[index].rotation);

            Image image = pieceObject.GetComponent<Image>();
            if (defaultPieces != null && defaultPieces.Length > 0 && defaultPieces[0] != null)
            {
                image.sprite = defaultPieces[0].sprite;
                image.material = defaultPieces[0].material;
                image.type = defaultPieces[0].type;
            }

            generatedPieces[index] = image;
        }

        return generatedPieces;
    }

    private void HideUnusedPieces(Image[] selectedPieces)
    {
        if (defaultPieces != null && defaultPieces != selectedPieces)
        {
            for (int pieceIndex = 0; pieceIndex < defaultPieces.Length; pieceIndex++)
            {
                if (defaultPieces[pieceIndex] != null)
                {
                    Color hiddenColor = defaultPieces[pieceIndex].color;
                    hiddenColor.a = 0f;
                    defaultPieces[pieceIndex].color = hiddenColor;
                }
            }
        }

        if (crosshairs == null)
        {
            return;
        }

        for (int definitionIndex = 0; definitionIndex < crosshairs.Length; definitionIndex++)
        {
            Image[] definitionPieces = crosshairs[definitionIndex]?.pieces;
            if (definitionPieces == null || definitionPieces == selectedPieces)
            {
                continue;
            }

            for (int pieceIndex = 0; pieceIndex < definitionPieces.Length; pieceIndex++)
            {
                if (definitionPieces[pieceIndex] != null)
                {
                    Color hiddenColor = definitionPieces[pieceIndex].color;
                    hiddenColor.a = 0f;
                    definitionPieces[pieceIndex].color = hiddenColor;
                }
            }
        }
    }

    private readonly struct PieceSpec
    {
        public readonly Vector2 position;
        public readonly Vector2 size;
        public readonly float rotation;

        public PieceSpec(Vector2 position, Vector2 size, float rotation)
        {
            this.position = position;
            this.size = size;
            this.rotation = rotation;
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
