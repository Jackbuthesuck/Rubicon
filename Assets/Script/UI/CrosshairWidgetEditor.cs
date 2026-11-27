using UnityEngine;
using UnityEngine.UI;

public sealed partial class CrosshairWidget
{
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            return;
        }

        defaultPieces = FindFallbackPieces();
        EnsureGeneratedDefinitions();
        CaptureEditorBaselines();
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

        RestoreEditorBaselines();
        defaultPieces = FindFallbackPieces();
        EnsureGeneratedDefinitions();
        CaptureEditorBaselines();
        UpdateEditorPreview();
        ApplyEditorPreview();
        MarkSceneDirty();
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
            new PieceSpec(new Vector2(0f, 56f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(32.666668f, 32.666668f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(56f, 0f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(32.666668f, -32.666668f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(0f, -56f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-32.666668f, -32.666668f), new Vector2(8f, 8f), 0f),
            new PieceSpec(new Vector2(-56f, 0f), new Vector2(8f, 8f), 0f)
        }, new[]
        {
            CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand,
            CrosshairPieceMode.Expand, CrosshairPieceMode.Expand, CrosshairPieceMode.Expand,
            CrosshairPieceMode.Expand
        }, new[] { true, true, true, true, true, true, true }, 34f, 66f);
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
        RestoreEditorBaselines();
        CrosshairDefinition definition = GetDefinition(editorPreviewCategory);
        Image[] previewPieces = definition != null ? definition.pieces : defaultPieces;
        if (previewPieces == null || previewPieces.Length == 0)
        {
            return;
        }

        pieces = previewPieces;
        CachePieceGeometry(definition);

        float minimum = definition != null ? definition.minimumGap : minimumGap;
        float maximum = definition != null ? definition.maximumGap : maximumGap;
        float runningSpeed = Mathf.Min(editorPreviewRunningSpeed, editorPreviewMaximumSpeed);
        float movementAmount = Mathf.InverseLerp(0f, Mathf.Max(0.01f, editorPreviewMaximumSpeed), runningSpeed);
        float recoilGapPreview = Mathf.Clamp(editorPreviewRecoilGap, 0f, maximum);
        float expandScale = Mathf.Max(1f, minimum + recoilGapPreview);

        for (int pieceIndex = 0; pieceIndex < previewPieces.Length; pieceIndex++)
        {
            Image piece = previewPieces[pieceIndex];
            RectTransform pieceRect = piece != null ? piece.rectTransform : null;
            if (piece == null || pieceRect == null || pieceIndex >= pieceRects.Length)
            {
                continue;
            }

            float scale = GetPieceScale(definition, pieceIndex, movementAmount, expandScale);
            pieceRect.anchoredPosition = GetPiecePosition(definition, pieceIndex, movementAmount, scale);
        }

        UpdateEditorPreview();
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

        int pieceCount = definition.pieces != null ? definition.pieces.Length : 0;
        if (pieceCount == 0 && definition.layoutPieces != null)
        {
            pieceCount = definition.layoutPieces.Length;
        }

        if (pieceCount > 0)
        {
            NormalizeArray(ref definition.pieceModes, pieceCount, CrosshairPieceMode.Expand);
            NormalizeArray(ref definition.runningVisible, pieceCount, true);
            NormalizeArray(ref definition.fadeWhenMoving, pieceCount, false);
            NormalizeArray(ref definition.fadeWhenFiring, pieceCount, false);
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

    private void SyncLayoutsFromPieceTransforms()
    {
        if (crosshairs == null)
        {
            return;
        }

        for (int definitionIndex = 0; definitionIndex < crosshairs.Length; definitionIndex++)
        {
            CrosshairDefinition definition = crosshairs[definitionIndex];
            if (definition == null || definition.pieces == null)
            {
                continue;
            }

            if (definition.layoutPieces == null || definition.layoutPieces.Length != definition.pieces.Length)
            {
                definition.layoutPieces = new CrosshairPieceDefinition[definition.pieces.Length];
            }

            for (int pieceIndex = 0; pieceIndex < definition.pieces.Length; pieceIndex++)
            {
                Image piece = definition.pieces[pieceIndex];
                if (piece == null)
                {
                    continue;
                }

                if (definition.layoutPieces[pieceIndex] == null)
                {
                    definition.layoutPieces[pieceIndex] = new CrosshairPieceDefinition();
                }

                RectTransform pieceRect = piece.rectTransform;
                definition.layoutPieces[pieceIndex].position = pieceRect.anchoredPosition;
                definition.layoutPieces[pieceIndex].size = pieceRect.sizeDelta;
                definition.layoutPieces[pieceIndex].rotation = pieceRect.localEulerAngles.z;
            }
        }
    }

    private void CacheAllPieceGeometry()
    {
        if (crosshairs == null)
        {
            return;
        }

        for (int definitionIndex = 0; definitionIndex < crosshairs.Length; definitionIndex++)
        {
            CrosshairDefinition definition = crosshairs[definitionIndex];
            if (definition == null || definition.pieces == null)
            {
                continue;
            }

            for (int pieceIndex = 0; pieceIndex < definition.pieces.Length; pieceIndex++)
            {
                Image piece = definition.pieces[pieceIndex];
                if (piece != null)
                {
                    RectTransform pieceRect = piece.rectTransform;
                    pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
                    pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
                    pieceRect.pivot = new Vector2(0.5f, 0.5f);
                }
            }
        }
    }

    private void CaptureEditorBaselines()
    {
        editorBaselinePositions.Clear();
        editorBaselineSizes.Clear();
        editorBaselineRotations.Clear();
        if (crosshairs == null)
        {
            return;
        }

        for (int definitionIndex = 0; definitionIndex < crosshairs.Length; definitionIndex++)
        {
            Image[] piecesToCapture = crosshairs[definitionIndex]?.pieces;
            if (piecesToCapture == null)
            {
                continue;
            }

            for (int pieceIndex = 0; pieceIndex < piecesToCapture.Length; pieceIndex++)
            {
                RectTransform rect = piecesToCapture[pieceIndex]?.rectTransform;
                if (rect == null)
                {
                    continue;
                }

                editorBaselinePositions[rect] = rect.anchoredPosition;
                editorBaselineSizes[rect] = rect.sizeDelta;
                editorBaselineRotations[rect] = rect.localRotation;
            }
        }

        if (defaultPieces != null)
        {
            for (int pieceIndex = 0; pieceIndex < defaultPieces.Length; pieceIndex++)
            {
                RectTransform rect = defaultPieces[pieceIndex]?.rectTransform;
                if (rect == null)
                {
                    continue;
                }

                editorBaselinePositions[rect] = rect.anchoredPosition;
                editorBaselineSizes[rect] = rect.sizeDelta;
                editorBaselineRotations[rect] = rect.localRotation;
            }
        }
    }

    private void RestoreEditorBaselines()
    {
        foreach (System.Collections.Generic.KeyValuePair<RectTransform, Vector2> entry in editorBaselinePositions)
        {
            if (entry.Key != null)
            {
                entry.Key.anchoredPosition = entry.Value;
            }
        }

        foreach (System.Collections.Generic.KeyValuePair<RectTransform, Vector2> entry in editorBaselineSizes)
        {
            if (entry.Key != null)
            {
                entry.Key.sizeDelta = entry.Value;
            }
        }

        foreach (System.Collections.Generic.KeyValuePair<RectTransform, Quaternion> entry in editorBaselineRotations)
        {
            if (entry.Key != null)
            {
                entry.Key.localRotation = entry.Value;
            }
        }
    }

    private static void NormalizeArray<T>(ref T[] array, int count, T fallback)
    {
        T[] normalized = new T[count];
        for (int index = 0; index < count; index++)
        {
            normalized[index] = array != null && index < array.Length ? array[index] : fallback;
        }
        array = normalized;
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
}
