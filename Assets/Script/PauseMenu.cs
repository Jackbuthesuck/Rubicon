using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class PauseMenu : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Canvas")]
    [SerializeField] private Canvas menuCanvas;

    [Header("Appearance")]
    [SerializeField] private int sortingOrder = 200;
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.84f);
    [SerializeField] private Color panelColor = new Color(0.012f, 0.016f, 0.016f, 0.98f);
    [SerializeField] private Color accentColor = new Color(1f, 0.34f, 0.04f, 1f);
    [SerializeField] private Font menuFont;
    [SerializeField] private float titlePulseSpeed = 0.5f;
    [SerializeField, Range(0f, 1f)] private float titlePulseMinOpacity = 0.15f;

    [Header("Editable Layout")]
    [SerializeField] private float railWidth = 760f;
    [SerializeField] private float railLeftMargin = 96f;
    [SerializeField] private float optionSpacing = 100f;
    [SerializeField] private Vector2 optionSize = new Vector2(440f, 82f);
    [SerializeField] private Vector2 footerOffset = new Vector2(0f, 44f);
    [SerializeField] private float footerHeight = 82f;

    private InputAction pauseAction;
    private InputAction cancelAction;
    private InputAction submitAction;
    private InputAction submitBackAction;
    private GameObject canvasObject;
    private GameObject menuRoot;
    private GameObject menuPanel;
    private Image promptBackground;
    private Image pauseOverlay;
    private Image pausePanelImage;
    private GameObject sceneTitleObject;
    private GameObject sceneTitleOverlay;
    private Text pauseTitle;
    private Text promptText;
    private bool promptInitialized;
    private bool promptController;
    private bool promptPlayStation;
    private CanvasGroup pauseTitleGroup;
    private Button[] menuButtons = new Button[0];
        private InventoryMenu inventoryMenu;
        private MenuController menuController;
        private bool inventoryOpen;
        private bool inventoryOpening;
        private int inventoryRequestId;
    private IDisposable inputDeviceSubscription;
    private bool isPaused;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BindSceneMenu();
        menuController = GetComponent<MenuController>();
        if (menuController == null)
        {
            menuController = gameObject.AddComponent<MenuController>();
        }
        InitializeInput();
        SetPaused(false);
    }

    private void BindSceneMenu()
    {
        if (menuCanvas == null)
        {
            menuCanvas = GetComponent<Canvas>();
        }

        if (menuCanvas == null)
        {
            Debug.LogWarning($"{nameof(PauseMenu)} needs a Canvas on the same GameObject or assigned in the Inspector.", this);
            return;
        }

        canvasObject = menuCanvas.gameObject;
        menuRoot = menuCanvas.transform.Find("PauseContent")?.gameObject;
        menuPanel = menuRoot?.transform.Find("Panel")?.gameObject;
        pauseOverlay = menuCanvas.transform.Find("Overlay")?.GetComponent<Image>();
        pausePanelImage = menuPanel?.GetComponent<Image>();
        sceneTitleObject = menuRoot?.transform.Find("Panel/Pause Canvas/SceneTitle")?.gameObject;
        PromotePersistentTitles();

        if (menuRoot == null || menuPanel == null)
        {
            Debug.LogWarning($"{nameof(PauseMenu)} expects PauseContent/Panel to exist in the scene Canvas.", this);
            return;
        }

        Text[] textElements = menuRoot.GetComponentsInChildren<Text>(true);
        for (int textIndex = 0; textIndex < textElements.Length; textIndex++)
        {
            if (textElements[textIndex].text == "PAUSED")
            {
                pauseTitle = textElements[textIndex];
                break;
            }
        }

        if (pauseTitle != null)
        {
            pauseTitleGroup = pauseTitle.GetComponent<CanvasGroup>();
            if (pauseTitleGroup == null)
            {
                pauseTitleGroup = pauseTitle.gameObject.AddComponent<CanvasGroup>();
            }
        }

        menuButtons = menuRoot.GetComponentsInChildren<Button>(true);
        ConfigureOptionsWidth();
        for (int buttonIndex = 0; buttonIndex < menuButtons.Length; buttonIndex++)
        {
            Button button = menuButtons[buttonIndex];
            ConfigureButtonVisual(button);
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = true;
            navigation.selectOnUp = menuButtons[(buttonIndex + menuButtons.Length - 1) % menuButtons.Length];
            navigation.selectOnDown = menuButtons[(buttonIndex + 1) % menuButtons.Length];
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            button.navigation = navigation;
            switch (button.name)
            {
                case "RESUME":
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => SetPaused(false));
                    break;
                case "CONTROLS":
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => ShowMessage("CONTROLS  /  WASD MOVE  /  RMB AIM  /  ESC PAUSE"));
                    break;
                case "INVENTORY":
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(OpenInventory);
                    break;
                case "RESTART":
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(RestartScene);
                    break;
            }
        }

        promptBackground = menuRoot.transform.Find("Panel/PromptBar")?.GetComponent<Image>();
        if (promptBackground != null)
        {
            promptBackground.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        promptText = promptBackground?.GetComponentInChildren<Text>(true);
        if (promptText != null)
        {
            promptText.supportRichText = true;
            UpdatePrompt(false, false);
        }

        ConfigureMenuWidth(promptText);

        EnsureEventSystem();
    }

    private void ConfigureOptionsWidth()
    {
        Transform optionsTransform = menuRoot.transform.Find("Panel/MenuLayout/Options");
        if (optionsTransform == null)
        {
            return;
        }

        LayoutElement optionsElement = optionsTransform.GetComponent<LayoutElement>();
        if (optionsElement != null)
        {
            optionsElement.flexibleWidth = 0f;
        }

        VerticalLayoutGroup optionsLayout = optionsTransform.GetComponent<VerticalLayoutGroup>();
        if (optionsLayout != null)
        {
            optionsLayout.childForceExpandWidth = false;
        }

        float longestWidth = 0f;
        Button[] buttons = optionsTransform.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                continue;
            }

            float labelWidth = LayoutUtility.GetPreferredWidth(label.rectTransform);
            longestWidth = Mathf.Max(longestWidth, labelWidth + 48f);
        }

        longestWidth = Mathf.Max(longestWidth, 160f);
        foreach (Button button in buttons)
        {
            LayoutElement buttonElement = button.GetComponent<LayoutElement>();
            if (buttonElement == null)
            {
                buttonElement = button.gameObject.AddComponent<LayoutElement>();
            }

            buttonElement.minWidth = longestWidth;
            buttonElement.preferredWidth = longestWidth;
            buttonElement.flexibleWidth = 0f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(optionsTransform as RectTransform);
    }

    private float GetPreferredTextWidth(Text text)
    {
        if (text == null)
        {
            return 0f;
        }

        TextGenerationSettings settings = text.GetGenerationSettings(Vector2.zero);
        return text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit;
    }

    private void ConfigureMenuWidth(Text promptText)
    {
        RectTransform panelRect = menuPanel.GetComponent<RectTransform>();
        RectTransform bodyRect = menuRoot.transform.Find("Panel/MenuLayout") as RectTransform;
        RectTransform optionsRect = menuRoot.transform.Find("Panel/MenuLayout/Options") as RectTransform;
        RectTransform promptRect = promptBackground != null ? promptBackground.GetComponent<RectTransform>() : null;
        if (panelRect == null || bodyRect == null || optionsRect == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(optionsRect);
        if (promptText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(promptText.rectTransform);
        }

        float optionsWidth = LayoutUtility.GetPreferredWidth(optionsRect);
        float promptWidth = GetPreferredTextWidth(promptText);
        float contentWidth = Mathf.Max(optionsWidth + 44f + 24f, promptWidth + 32f);
        float panelWidth = Mathf.Max(320f, contentWidth + 72f);

        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 72f);
        if (promptRect != null)
        {
            promptRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth - 72f);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);
    }

    private void InitializeInput()
    {
        if (inputActions != null)
        {
            pauseAction = inputActions.FindAction("Player/Pause", true);
            cancelAction = inputActions.FindAction("UI/Cancel", false);
            submitAction = inputActions.FindAction("UI/Submit A", false);
            submitBackAction = inputActions.FindAction("UI/Submit B", false);
        }
        else
        {
            Debug.LogWarning($"{nameof(PauseMenu)} needs an InputActionAsset assigned.", this);
        }

    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BindSceneMenu();
        InitializeInput();
        pauseAction?.Enable();
        cancelAction?.Enable();
        submitAction?.Enable();
        submitBackAction?.Enable();
        inputDeviceSubscription = InputSystem.onAnyButtonPress.Subscribe(new InputControlObserver(OnInputDeviceUsed));
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        pauseAction?.Disable();
        cancelAction?.Disable();
        submitAction?.Disable();
        submitBackAction?.Disable();
        inputDeviceSubscription?.Dispose();
        inputDeviceSubscription = null;
        SetPaused(false);
    }

    private void Update()
    {
        bool keyboardPausePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if ((pauseAction != null && pauseAction.WasPressedThisFrame()) || keyboardPausePressed)
        {
            if (inventoryOpen)
            {
                CloseInventory();
            }
            else
            {
                SetPaused(!isPaused);
            }
        }

        if (isPaused && cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            if (inventoryOpen)
            {
                CloseInventory();
            }
            else
            {
                SetPaused(false);
            }
            return;
        }

        if (isPaused && submitAction != null && submitAction.WasPressedThisFrame())
        {
            if (inventoryOpen)
            {
                return;
            }

            GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
            Button selectedButton = selectedObject != null ? selectedObject.GetComponent<Button>() : null;
            selectedButton?.onClick.Invoke();
            return;
        }

        if (isPaused)
        {
            if (inventoryOpen)
            {
                UpdateTitlePulse();
                return;
            }

            DetectStickInput();
            UpdateButtonVisuals();
            UpdateTitlePulse();
        }
    }

    private void OnApplicationQuit()
    {
        Time.timeScale = 1f;
    }

    private void OnInputDeviceUsed(InputControl control)
    {
        if (control.device is Gamepad gamepad)
        {
            UpdatePrompt(true, IsPlayStationDevice(gamepad));
        }
        else if (control.device is Keyboard)
        {
            UpdatePrompt(false, false);
        }
    }

    private void DetectStickInput()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.04f
                || gamepad.rightStick.ReadValue().sqrMagnitude > 0.04f)
            {
                UpdatePrompt(true, IsPlayStationDevice(gamepad));
            }
        }
    }

    private bool IsPlayStationDevice(InputDevice device)
    {
        string deviceDescription = $"{device.layout} {device.displayName} {device.description.manufacturer} {device.description.product} {device.description.interfaceName}".ToLowerInvariant();
        return deviceDescription.Contains("dualshock")
            || deviceDescription.Contains("dualsense")
            || deviceDescription.Contains("playstation")
            || deviceDescription.Contains("ps4")
            || deviceDescription.Contains("ps5")
            || deviceDescription.Contains("wireless controller");
    }

    private void UpdatePrompt(bool controller, bool playStation)
    {
        if (promptText == null)
        {
            return;
        }

        if (promptInitialized && promptController == controller && promptPlayStation == playStation)
        {
            return;
        }

        promptInitialized = true;
        promptController = controller;
        promptPlayStation = playStation;

        promptText.text = controller
            ? playStation
                ? "<color=#E53935>○</color>  <color=#FFFFFF>BACK</color>    <color=#1976D2>×</color>  <color=#FFFFFF>SELECT</color>    <color=#9E9E9E>D-PAD</color>  <color=#FFFFFF>NAVIGATE</color>"
                : "<color=#E53935>B</color>  <color=#FFFFFF>BACK</color>    <color=#43A047>A</color>  <color=#FFFFFF>SELECT</color>    <color=#9E9E9E>DPAD</color>  <color=#FFFFFF>NAVIGATE</color>"
            : "<color=#FF0000>ESC</color>  <color=#FFFFFF>BACK</color>    <color=#FF0000>ENTER</color>  <color=#FFFFFF>SELECT</color>    <color=#FF0000>W/S</color>  <color=#FFFFFF>NAVIGATE</color>";
        promptText.color = Color.white;
        ConfigureMenuWidth(promptText);
    }

    private sealed class InputControlObserver : IObserver<InputControl>
    {
        private readonly Action<InputControl> onNext;

        public InputControlObserver(Action<InputControl> onNext)
        {
            this.onNext = onNext;
        }

        public void OnNext(InputControl value)
        {
            onNext(value);
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private void ConfigureButtonVisual(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.black;
        colors.highlightedColor = Color.red;
        colors.pressedColor = Color.red;
        colors.selectedColor = Color.red;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        button.colors = colors;
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (inputActions != null)
        {
            inputModule.actionsAsset = inputActions;
        }
    }

    private void UpdateButtonVisuals()
    {
        GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
        foreach (Button button in menuButtons)
        {
            bool selected = button.gameObject == selectedObject;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = selected ? Color.black : Color.white;
            }
        }
    }

    private void UpdateTitlePulse()
    {
        if (pauseTitle == null)
        {
            return;
        }

        if (inventoryOpen || inventoryOpening)
        {
            SetPauseTitleOpacity(1f);
            return;
        }

        float sawtooth = Mathf.Repeat(Time.unscaledTime * titlePulseSpeed, 1f);
        float opacity = Mathf.Lerp(titlePulseMinOpacity, 1f, sawtooth);
        SetPauseTitleOpacity(opacity);
    }

    private void SetPauseTitleOpacity(float opacity)
    {
        if (pauseTitleGroup != null)
        {
            pauseTitleGroup.alpha = opacity;
        }
        else
        {
            Color titleColor = pauseTitle.color;
            titleColor.a = opacity;
            pauseTitle.color = titleColor;
        }
    }

    private void BuildMenu()
    {
        if (menuCanvas == null)
        {
            menuCanvas = GetComponent<Canvas>();
        }

        if (menuCanvas == null)
        {
            Debug.LogWarning($"{nameof(PauseMenu)} needs a Canvas on the same GameObject or assigned in the Inspector.", this);
            return;
        }

        canvasObject = menuCanvas.gameObject;
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = sortingOrder;
        menuCanvas.enabled = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        menuRoot = new GameObject("PauseContent", typeof(RectTransform));
        menuRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreateImage(rootRect, "Overlay", overlayColor, Vector2.zero, Vector2.one);

        menuPanel = CreateImage(rootRect, "Panel", panelColor, Vector2.zero, new Vector2(railWidth, 1080f));
        RectTransform panelRect = menuPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(railLeftMargin, 0f);
        panelRect.sizeDelta = new Vector2(railWidth, 1080f);

        GameObject bodyObject = new GameObject("MenuLayout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.SetParent(panelRect, false);
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(0f, 1f);
        bodyRect.pivot = new Vector2(0f, 1f);
        bodyRect.anchoredPosition = new Vector2(36f, -34f);
        bodyRect.sizeDelta = new Vector2(railWidth - 72f, 1080f - footerHeight - 68f);

        HorizontalLayoutGroup bodyLayout = bodyObject.GetComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 24f;
        bodyLayout.childAlignment = TextAnchor.MiddleLeft;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;

        GameObject titleObject = new GameObject("VerticalTitle", typeof(RectTransform), typeof(LayoutElement));
        titleObject.transform.SetParent(bodyRect, false);
        LayoutElement titleLayout = titleObject.GetComponent<LayoutElement>();
        titleLayout.minWidth = 44f;
        titleLayout.preferredWidth = 44f;
        titleLayout.flexibleWidth = 0f;
        RectTransform titleSlotRect = titleObject.GetComponent<RectTransform>();
        titleSlotRect.anchorMin = new Vector2(0f, 1f);
        titleSlotRect.anchorMax = new Vector2(0f, 1f);
        titleSlotRect.pivot = new Vector2(0f, 1f);

        GameObject titleTextObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        titleTextObject.transform.SetParent(titleSlotRect, false);
        Text pauseTitle = titleTextObject.GetComponent<Text>();
        pauseTitle.text = "PAUSED";
        pauseTitle.font = menuFont;
        pauseTitle.fontSize = 30;
        pauseTitle.fontStyle = FontStyle.Bold;
        pauseTitle.alignment = TextAnchor.MiddleRight;
        pauseTitle.color = accentColor;
        pauseTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
        pauseTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
        pauseTitle.rectTransform.pivot = new Vector2(1f, 1f);
        pauseTitle.rectTransform.anchoredPosition = Vector2.zero;
        pauseTitle.rectTransform.sizeDelta = new Vector2(136f, 42f);
        pauseTitle.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);

        GameObject optionsObject = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup));
        optionsObject.transform.SetParent(bodyRect, false);
        VerticalLayoutGroup optionsLayout = optionsObject.GetComponent<VerticalLayoutGroup>();
        LayoutElement optionsLayoutElement = optionsObject.AddComponent<LayoutElement>();
        optionsLayout.spacing = optionSpacing;
        optionsLayout.childAlignment = TextAnchor.MiddleLeft;
        optionsLayout.childControlWidth = true;
        optionsLayout.childControlHeight = true;
        optionsLayout.childForceExpandWidth = false;
        optionsLayout.childForceExpandHeight = false;

        CreateLayoutLabel(optionsObject.transform, "FIELD OPERATIONS", 18, Color.gray, 44f);

        Button resumeButton = CreateButton(optionsObject.transform, "RESUME");
        resumeButton.onClick.AddListener(() => SetPaused(false));

        Button controlsButton = CreateButton(optionsObject.transform, "CONTROLS");
        controlsButton.onClick.AddListener(() => ShowMessage("CONTROLS  /  WASD MOVE  /  RMB AIM  /  ESC PAUSE"));

        Button inventoryButton = CreateButton(optionsObject.transform, "INVENTORY");
        inventoryButton.onClick.AddListener(OpenInventory);

        Button restartButton = CreateButton(optionsObject.transform, "RESTART");
        restartButton.onClick.AddListener(RestartScene);

        CreatePrompt(panelRect);
        if (Application.isPlaying)
        {
            CreateEventSystemIfNeeded();
        }
    }

    private void SetPaused(bool paused)
    {
        inventoryRequestId++;
        isPaused = paused;
        inventoryOpen = false;
        Time.timeScale = paused ? 0f : 1f;

        if (menuRoot != null && Application.isPlaying)
        {
            menuRoot.SetActive(paused);
            inventoryOpen = false;
            inventoryMenu?.Close();
            menuController?.CloseAllChildren();
            SetPauseElementsForInventory(false);
            if (paused && EventSystem.current != null && menuButtons.Length > 0)
            {
                EventSystem.current.SetSelectedGameObject(menuButtons[0].gameObject);
            }
        }
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ShowMessage(string message)
    {
        Debug.Log(message, this);
    }

    private void OpenInventory()
    {
        if (!isPaused || inventoryOpen || inventoryOpening)
        {
            return;
        }

        GameObject parentSelection = EventSystem.current?.currentSelectedGameObject;
        inventoryRequestId++;
        StartCoroutine(OpenInventoryRoutine(parentSelection, inventoryRequestId));
    }

    private IEnumerator OpenInventoryRoutine(GameObject parentSelection, int requestId)
    {
        inventoryOpening = true;
        SetPauseElementsForInventory(true);

        inventoryMenu = null;
        const string inventorySceneName = "InventoryMenu";
        Scene inventoryScene = SceneManager.GetSceneByName(inventorySceneName);
        if (!inventoryScene.isLoaded)
        {
            if (!Application.CanStreamedLevelBeLoaded(inventorySceneName))
            {
                Debug.LogWarning("InventoryMenu.unity is not available in Build Settings.", this);
                inventoryOpening = false;
                SetPauseElementsForInventory(false);
                yield break;
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(inventorySceneName, LoadSceneMode.Additive);
            if (loadOperation != null)
            {
                yield return loadOperation;
            }

            if (requestId != inventoryRequestId || !isPaused)
            {
                inventoryOpening = false;
                SetPauseElementsForInventory(false);
                yield break;
            }

            inventoryScene = SceneManager.GetSceneByName(inventorySceneName);
        }

        if (inventoryScene.IsValid() && inventoryScene.isLoaded)
        {
            GameObject[] roots = inventoryScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length && inventoryMenu == null; rootIndex++)
            {
                inventoryMenu = roots[rootIndex].GetComponentInChildren<InventoryMenu>(true);
            }
        }

        if (inventoryMenu == null)
        {
            inventoryOpening = false;
            SetPauseElementsForInventory(false);
            yield break;
        }

        inventoryMenu.SetInputActions(inputActions);
        inventoryMenu.Open();
        inventoryOpen = inventoryMenu.IsOpen
            && menuController != null
            && menuController.OpenChild(inventoryMenu.ContentRoot, inventoryMenu.FirstSelectable, parentSelection);
        inventoryOpening = false;

        if (!inventoryOpen)
        {
            inventoryMenu.Close();
            SetPauseElementsForInventory(false);
            yield break;
        }

        SetPauseElementsForInventory(inventoryOpen);
    }

    private void CloseInventory()
    {
        if (inventoryOpening)
        {
            inventoryRequestId++;
            inventoryOpening = false;
            SetPauseElementsForInventory(false);
            return;
        }

        inventoryOpen = false;
        menuController?.CloseChild();
        inventoryMenu?.Close();
        SetPauseElementsForInventory(false);
        menuRoot?.SetActive(isPaused);
    }

    private void SetPauseElementsForInventory(bool inventoryVisible)
    {
        if (menuRoot == null)
        {
            return;
        }

        if (!inventoryVisible)
        {
            if (sceneTitleOverlay != null)
            {
                sceneTitleOverlay.SetActive(false);
            }

            if (pauseOverlay != null)
            {
                pauseOverlay.enabled = true;
            }

            if (pausePanelImage != null)
            {
                pausePanelImage.enabled = true;
            }

            menuRoot.transform.Find("Panel/PromptBar")?.gameObject.SetActive(true);
            foreach (Button button in menuRoot.GetComponentsInChildren<Button>(true))
            {
                button.gameObject.SetActive(true);
            }
            foreach (Text text in menuRoot.GetComponentsInChildren<Text>(true))
            {
                text.gameObject.SetActive(true);
            }
            return;
        }

        if (pauseOverlay != null)
        {
            pauseOverlay.enabled = false;
        }

        if (pausePanelImage != null)
        {
            pausePanelImage.enabled = false;
        }

        if (sceneTitleOverlay != null)
        {
            sceneTitleOverlay.SetActive(true);
        }

        foreach (Button button in menuRoot.GetComponentsInChildren<Button>(true))
        {
            button.gameObject.SetActive(false);
        }
        menuRoot.transform.Find("Panel/PromptBar")?.gameObject.SetActive(false);

        foreach (Text text in menuRoot.GetComponentsInChildren<Text>(true))
        {
            bool keepText = text.text == "PAUSED" || text.text.StartsWith("ID TEST01");
            text.gameObject.SetActive(keepText);
        }
    }

    private void PromotePersistentTitles()
    {
        if (menuRoot == null)
        {
            return;
        }

        if (sceneTitleObject != null && sceneTitleOverlay == null)
        {
            sceneTitleOverlay = Instantiate(sceneTitleObject, menuCanvas.transform, true);
            sceneTitleOverlay.name = "SceneTitleOverlay";
            PromoteTitleCanvas(sceneTitleOverlay.transform);
            sceneTitleOverlay.SetActive(false);
        }

        foreach (Text text in menuRoot.GetComponentsInChildren<Text>(true))
        {
            if (text.text == "PAUSED")
            {
                PromoteTitleCanvas(text.transform);
                break;
            }
        }
    }

    private void PromoteTitleCanvas(Transform titleTransform)
    {
        if (titleTransform == null)
        {
            return;
        }

        Canvas titleCanvas = titleTransform.GetComponent<Canvas>();
        if (titleCanvas == null)
        {
            titleCanvas = titleTransform.gameObject.AddComponent<Canvas>();
        }

        titleCanvas.overrideSorting = true;
        titleCanvas.sortingOrder = 400;
    }

    private void CreatePrompt(RectTransform parent)
    {
        GameObject promptObject = CreateImage(parent, "PromptBar", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.zero);
        RectTransform promptRect = promptObject.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = new Vector2(1f, 0f);
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-footerOffset.x, footerOffset.y);
        promptRect.sizeDelta = new Vector2(0f, footerHeight);

        Text prompt = CreateLabel(promptRect, "ESC  BACK        ENTER  SELECT        W/S  NAVIGATE", Vector2.zero, new Vector2(650f, 70f), 18, TextAnchor.MiddleRight, Color.white);
        prompt.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        prompt.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        prompt.rectTransform.pivot = new Vector2(1f, 0.5f);
        prompt.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void CreateEventSystemIfNeeded()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private GameObject CreateImage(Transform parent, string objectName, Color color, Vector2 anchor, Vector2 size)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor == Vector2.zero ? Vector2.zero : anchor;
        rect.anchorMax = anchor == Vector2.one ? Vector2.one : anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }

    private Text CreateLabel(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject labelObject = new GameObject(text, typeof(RectTransform), typeof(Text));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text label = labelObject.GetComponent<Text>();
        label.text = text;
        label.font = menuFont;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        return label;
    }

    private Text CreateLayoutLabel(Transform parent, string text, int fontSize, Color color, float height)
    {
        Text label = CreateLabel(parent, text, Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleLeft, color);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
        return label;
    }

    private Button CreateButton(Transform parent, string text)
    {
        GameObject buttonObject = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = optionSize;

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = Mathf.Max(optionSize.x, menuFont != null ? menuFont.fontSize * text.Length * 0.7f + 48f : optionSize.x);
        layout.preferredHeight = optionSize.y;
        layout.flexibleWidth = 0f;

        Image background = buttonObject.GetComponent<Image>();
        background.color = accentColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = accentColor;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.18f, 0.02f, 1f);
        button.colors = colors;

        Text buttonLabel = CreateLabel(buttonObject.transform, text, Vector2.zero, optionSize - new Vector2(10f, 8f), 30, TextAnchor.MiddleCenter, Color.white);
        buttonLabel.rectTransform.anchorMin = Vector2.zero;
        buttonLabel.rectTransform.anchorMax = Vector2.one;
        buttonLabel.rectTransform.offsetMin = Vector2.zero;
        buttonLabel.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}