using UnityEngine;
using UnityEngine.UI;

public sealed class HUD : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerVitals vitals;
    [SerializeField] private WeaponController weaponController;

    [Header("Player Display")]
    [SerializeField] private string playerName = "PLAYER";
    [SerializeField] private string weaponName = "PISTOL";
    [SerializeField, Min(0)] private int reserveAmmo = 120;
    [SerializeField, Min(0)] private int magazineAmmo = 12;
    [SerializeField, Min(0)] private int chamberAmmo = 1;

    [Header("Canvas")]
    [SerializeField] private Canvas hudCanvas;

    [Header("Layout")]
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private Vector2 barSize = new Vector2(420f, 38f);
    [SerializeField] private Vector2 barSpacing = new Vector2(0f, 14f);
    [SerializeField] private Vector2 barMargin = new Vector2(48f, 48f);

    [Header("Colors")]
    [SerializeField] private Color healthColor = new Color(0.85f, 0.12f, 0.1f, 1f);
    [SerializeField] private Color staminaColor = new Color(0.2f, 0.75f, 0.35f, 1f);
    [SerializeField] private Color barBackgroundColor = new Color(0.03f, 0.04f, 0.04f, 0.8f);

    [Header("Font")]
    [SerializeField] private Font hudFont;
    private BarWidget healthBar;
    private BarWidget staminaBar;
    private CrosshairWidget crosshair;
    private AmmoWidget ammoWidget;

    private void Awake()
    {
        ResolveTarget();
        BindSceneUI();
    }

    private void OnEnable()
    {
        if (vitals != null)
        {
            vitals.HealthChanged += OnHealthChanged;
            vitals.StaminaChanged += OnStaminaChanged;
        }
    }

    private void OnDisable()
    {
        if (vitals != null)
        {
            vitals.HealthChanged -= OnHealthChanged;
            vitals.StaminaChanged -= OnStaminaChanged;
        }
    }

    private void Update()
    {
        if (player != null && crosshair != null)
        {
            crosshair.SetAiming(player.IsAiming);
        }

        if (weaponController != null && ammoWidget != null)
        {
            ammoWidget.SetDisplay(
                weaponController.DisplayName,
                weaponController.ReserveAmmo,
                weaponController.MagazineAmmo,
                weaponController.ChamberAmmo);
        }
    }

    private void ResolveTarget()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (vitals == null && player != null)
        {
            vitals = player.GetComponent<PlayerVitals>();
        }
        if (weaponController == null)
        {
            weaponController = FindFirstObjectByType<WeaponController>();
        }
    }

    private void BindSceneUI()
    {
        if (hudCanvas == null)
        {
            hudCanvas = GetComponent<Canvas>();
        }

        if (hudCanvas == null)
        {
            Debug.LogWarning($"{nameof(HUD)} needs a Canvas on the same GameObject or assigned in the Inspector.", this);
            return;
        }

        hudCanvas.enabled = true;
        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = sortingOrder;

        Transform content = hudCanvas.transform.Find("HUDContent");
        if (content == null)
        {
            Debug.LogWarning($"{nameof(HUD)} expects HUDContent to exist under the Canvas.", this);
            return;
        }

        Graphic[] graphics = content.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic.GetComponent<CanvasRenderer>() == null)
            {
                graphic.gameObject.AddComponent<CanvasRenderer>();
            }
        }

        Font fallbackFont = hudFont != null ? hudFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        Text[] labels = content.GetComponentsInChildren<Text>(true);
        foreach (Text label in labels)
        {
            if (label.font == null)
            {
                label.font = fallbackFont;
            }

            if (label.fontSize <= 0)
            {
                label.fontSize = 24;
            }

            if (label.color.a <= 0f)
            {
                label.color = Color.white;
            }

            label.enabled = true;
            label.SetAllDirty();
        }

        healthBar = FindHudComponent<BarWidget>(content, "HealthBar");
        staminaBar = FindHudComponent<BarWidget>(content, "StaminaBar");
        crosshair = FindHudComponent<CrosshairWidget>(content, "Crosshair");
        ammoWidget = FindHudComponent<AmmoWidget>(content, "AmmoGroup");

        if (healthBar == null || staminaBar == null || crosshair == null)
        {
            Debug.LogWarning($"{nameof(HUD)} expects HUDContent/HealthBar, HUDContent/StaminaBar, and HUDContent/Crosshair in the scene.", this);
        }

        if (healthBar != null)
        {
            Image healthFill = healthBar.transform.Find("Fill")?.GetComponent<Image>();
            Text healthLabel = healthBar.transform.Find("Value")?.GetComponent<Text>();
            healthBar.Initialize(healthFill, healthLabel);
        }

        if (staminaBar != null)
        {
            Image staminaFill = staminaBar.transform.Find("Fill")?.GetComponent<Image>();
            Text staminaLabel = staminaBar.transform.Find("Value")?.GetComponent<Text>();
            staminaBar.Initialize(staminaFill, staminaLabel);
        }

        if (crosshair != null)
        {
            Transform crosshairTransform = crosshair.transform;
            Image[] pieces = new Image[4];
            for (int pieceIndex = 0; pieceIndex < pieces.Length; pieceIndex++)
            {
                pieces[pieceIndex] = crosshairTransform.Find($"Piece{pieceIndex + 1}")?.GetComponent<Image>();
            }

            crosshair.Initialize(pieces);
        }

        Text playerNameText = FindHudComponent<Text>(content, "PlayerName");
        if (playerNameText != null)
        {
            playerNameText.resizeTextForBestFit = true;
            playerNameText.resizeTextMinSize = 18;
            playerNameText.resizeTextMaxSize = 32;
            playerNameText.text = playerName;
            playerNameText.SetAllDirty();
        }

        if (ammoWidget != null)
        {
            Text reserveText = FindHudComponent<Text>(content, "ReserveAmmo");
            Text magazineDisplay = FindHudComponent<Text>(content, "MagazineDisplay");
            Text weaponText = FindHudComponent<Text>(content, "WeaponName");
            ammoWidget.Initialize(reserveText, magazineDisplay, weaponText);
            ammoWidget.SetDisplay(weaponName, reserveAmmo, magazineAmmo, chamberAmmo);
        }

        if (vitals != null)
        {
            healthBar?.SetValue(vitals.Health, vitals.MaximumHealth);
            staminaBar?.SetValue(vitals.Stamina, vitals.MaximumStamina);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static T FindHudComponent<T>(Transform root, string objectName) where T : Component
    {
        return FindHudTransform(root, objectName)?.GetComponent<T>();
    }

    private static Transform FindHudTransform(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    private void BuildRuntimeUI()
    {
        GameObject canvasObject = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        healthBar = CreateBar(root, "Health", healthColor, 0);
        staminaBar = CreateBar(root, "Stamina", staminaColor, 1);
        crosshair = CreateCrosshair(root);

        if (vitals != null)
        {
            healthBar.SetValue(vitals.Health, vitals.MaximumHealth);
            staminaBar.SetValue(vitals.Stamina, vitals.MaximumStamina);
        }
    }

    private BarWidget CreateBar(RectTransform parent, string label, Color fillColor, int index)
    {
        GameObject barObject = new GameObject(label + "Bar", typeof(RectTransform), typeof(Image), typeof(BarWidget));
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.SetParent(parent, false);
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(barMargin.x, -barMargin.y - index * (barSize.y + barSpacing.y));
        barRect.sizeDelta = barSize;

        BarWidget widget = barObject.GetComponent<BarWidget>();
        widget.SetVisuals(fillColor, barBackgroundColor);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(barRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.color = fillColor;

        GameObject labelObject = new GameObject("Value", typeof(RectTransform), typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(barRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-8f, 0f);

        Text valueText = labelObject.GetComponent<Text>();
        valueText.alignment = TextAnchor.MiddleLeft;
        valueText.font = hudFont;
        valueText.fontSize = 20;
        valueText.color = Color.white;
        widget.Initialize(fill, valueText);
        return widget;
    }

    private CrosshairWidget CreateCrosshair(RectTransform parent)
    {
        GameObject crosshairObject = new GameObject("Crosshair", typeof(RectTransform), typeof(CrosshairWidget));
        RectTransform crosshairRect = crosshairObject.GetComponent<RectTransform>();
        crosshairRect.SetParent(parent, false);
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.anchoredPosition = Vector2.zero;
        crosshairRect.sizeDelta = new Vector2(72f, 72f);

        Image[] pieces = new Image[4];
        pieces[0] = CreateCrosshairPiece(crosshairRect, new Vector2(0f, 24f), new Vector2(4f, 16f));
        pieces[1] = CreateCrosshairPiece(crosshairRect, new Vector2(0f, -24f), new Vector2(4f, 16f));
        pieces[2] = CreateCrosshairPiece(crosshairRect, new Vector2(-24f, 0f), new Vector2(16f, 4f));
        pieces[3] = CreateCrosshairPiece(crosshairRect, new Vector2(24f, 0f), new Vector2(16f, 4f));

        CrosshairWidget widget = crosshairObject.GetComponent<CrosshairWidget>();
        widget.Initialize(pieces);
        return widget;
    }

    private Image CreateCrosshairPiece(RectTransform parent, Vector2 position, Vector2 size)
    {
        GameObject pieceObject = new GameObject("Piece", typeof(RectTransform), typeof(Image));
        RectTransform pieceRect = pieceObject.GetComponent<RectTransform>();
        pieceRect.SetParent(parent, false);
        pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
        pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
        pieceRect.anchoredPosition = position;
        pieceRect.sizeDelta = size;
        return pieceObject.GetComponent<Image>();
    }

    private void OnHealthChanged(float current, float maximum)
    {
        healthBar?.SetValue(current, maximum);
    }

    private void OnStaminaChanged(float current, float maximum)
    {
        staminaBar?.SetValue(current, maximum);
    }
}