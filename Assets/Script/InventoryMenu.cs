using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public sealed class InventoryMenu : MonoBehaviour
{
    private const int MaxWeaponsPerTab = 8;
    [SerializeField] private Canvas inventoryCanvas;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Transform equipmentList;
    [SerializeField] private Text detailsText;
    [SerializeField] private Text pageTitle;
    [SerializeField] private Button[] equipmentSlots = new Button[0];
    [SerializeField] private Button[] mainTabButtons = new Button[0];
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private int sortingOrder = 300;
    [SerializeField] private bool debugInputLogs = true;
    [SerializeField, Range(0f, 1f)] private float titlePulsePhaseOffset = 0.37f;
    [SerializeField, Range(0f, 1f)] private float titlePulseMinimumOpacity = 0.15f;

    private readonly List<WeaponController> visibleWeapons = new List<WeaponController>();
    private readonly Dictionary<Button, Image> equippedMarkers = new Dictionary<Button, Image>();
    private string selectedMainTab = "PRIMARY";
    private InputAction leftPageAction;
    private InputAction rightPageAction;
    private InputAction navigateAction;
    private InputAction submitAction;
    private CanvasGroup pageTitleGroup;
    private PauseMenu pauseMenu;
    private Image promptBackground;
    private Text promptText;
    private Image equippedMarkerTemplate;
    private IDisposable inputDeviceSubscription;
    private bool promptInitialized;
    private bool promptController;
    private bool promptPlayStation;
    private float nextDebugStatusTime;
    private Coroutine refreshCoroutine;

    public bool IsOpen { get; private set; }
    public GameObject ContentRoot => contentRoot != null ? contentRoot.gameObject : null;
    public GameObject FirstSelectable => GetFirstActiveSlot()?.gameObject
        ?? GetFirstActiveTab()?.gameObject;

    private void Awake()
    {
        if (inventoryCanvas == null)
        {
            inventoryCanvas = GetComponent<Canvas>();
        }

        if (inventoryCanvas != null)
        {
            inventoryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            inventoryCanvas.sortingOrder = sortingOrder;
        }

        BindSceneLayout();
        BindPageActions();
        pauseMenu = FindFirstObjectByType<PauseMenu>();
        BindPrompt();
        Close();
    }

    private void OnEnable()
    {
        inputDeviceSubscription = InputSystem.onAnyButtonPress.Subscribe(new InputControlObserver(OnInputDeviceUsed));
    }

    private void OnDisable()
    {
        inputDeviceSubscription?.Dispose();
        inputDeviceSubscription = null;
    }

    public void SetInputActions(InputActionAsset actions)
    {
        if (inputActions == actions)
        {
            return;
        }

        DisablePageActions();
        inputActions = actions;
        BindPageActions();
        if (IsOpen)
        {
            EnablePageActions();
            navigateAction?.Enable();
            submitAction?.Enable();
        }
    }

    public void Open()
    {
        BindSceneLayout();
        RefreshSceneControls();
        IsOpen = ContentRoot != null;
        if (ContentRoot != null)
        {
            ContentRoot.SetActive(IsOpen);
        }
        promptBackground?.gameObject.SetActive(IsOpen);

        if (IsOpen)
        {
            EnsureEventSystemInputModule();
            EnablePageActions();
            navigateAction?.Enable();
            submitAction?.Enable();
        }

        if (IsOpen && EventSystem.current != null)
        {
            Button firstSlot = GetFirstActiveSlot() ?? GetFirstActiveTab();
            if (firstSlot != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSlot.gameObject);
            }
        }

        LogInputStatus(true);

        if (refreshCoroutine != null)
        {
            refreshCoroutine = null;
        }

        refreshCoroutine = StartCoroutine(RefreshWhenLoadoutReady());
    }

    private void EnsureEventSystemInputModule()
    {
        if (inputActions == null)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            eventSystem = FindFirstObjectByType<EventSystem>();
        }

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

        eventSystem.sendNavigationEvents = true;
        inputModule.enabled = false;
        inputModule.actionsAsset = inputActions;
        inputModule.enabled = true;
        LogInput($"Input module ready. eventSystem={eventSystem.name} navigationEvents={eventSystem.sendNavigationEvents} "
            + $"moduleEnabled={inputModule.enabled} asset={inputActions.name}");
    }

    public void Close()
    {
        DisablePageActions();
        if (refreshCoroutine != null)
        {
            refreshCoroutine = null;
        }
        IsOpen = false;
        if (ContentRoot != null)
        {
            ContentRoot.SetActive(false);
        }
        promptBackground?.gameObject.SetActive(false);
    }

    private void BindPageActions()
    {
        leftPageAction = inputActions?.FindAction("UI/L Page", false);
        rightPageAction = inputActions?.FindAction("UI/R Page", false);
        navigateAction = inputActions?.FindAction("UI/Navigate", false);
        submitAction = inputActions?.FindAction("UI/Submit A", false);
    }

    private void EnablePageActions()
    {
        leftPageAction?.Enable();
        rightPageAction?.Enable();
    }

    private void DisablePageActions()
    {
        leftPageAction?.Disable();
        rightPageAction?.Disable();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        LogInputStatus(false);
        UpdateTitlePulse();
        UpdateButtonVisuals();

        if (WasLeftPagePressed())
        {
            LogInput($"L Page pressed. selectedBefore={GetSelectedName()}");
            SelectAdjacentMainTab(1);
            LogInput($"L Page selectedAfter={GetSelectedName()}");
        }
        else if (WasRightPagePressed())
        {
            LogInput($"R Page pressed. selectedBefore={GetSelectedName()}");
            SelectAdjacentMainTab(-1);
            LogInput($"R Page selectedAfter={GetSelectedName()}");
        }

        bool submitPressed = submitAction != null && submitAction.WasPressedThisFrame();
        if (submitAction == null || !submitAction.enabled)
        {
            submitPressed = (Keyboard.current != null
                && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        if (submitPressed)
        {
            GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
            Button selectedButton = selectedObject?.GetComponent<Button>();
            LogInput($"Submit A pressed. selected={GetObjectName(selectedObject)} button={GetObjectName(selectedButton?.gameObject)}");
            selectedButton?.onClick.Invoke();
        }
    }

    private IEnumerator RefreshWhenLoadoutReady()
    {
        while (IsOpen)
        {
            WeaponLoadout loadout = FindLoadout();
            if (loadout != null && loadout.IsReady)
            {
                RefreshSceneControls();
                refreshCoroutine = null;
                yield break;
            }

            yield return null;
        }

        refreshCoroutine = null;
    }

    private void BindSceneLayout()
    {
        if (inventoryCanvas == null)
        {
            return;
        }

        if (contentRoot == null)
        {
            contentRoot = FindDescendant(inventoryCanvas.transform, "InventoryContent");
        }

        if (contentRoot == null)
        {
            Debug.LogError("InventoryMenu.unity needs an InventoryMenuCanvas/InventoryContent hierarchy.", this);
            return;
        }

        if (equipmentList == null)
        {
            equipmentList = FindDescendant(contentRoot, "Options");
        }

        if (detailsText == null)
        {
            detailsText = FindText("FIELD OPERATIONS");
        }

        if (pageTitle == null)
        {
            pageTitle = FindText("INVENTORY");
        }

        equipmentSlots = equipmentList != null
            ? equipmentList.GetComponentsInChildren<Button>(true)
            : new Button[0];
        EnsureEquipmentSlots();
        equippedMarkerTemplate = equipmentSlots.Length > 0 && equipmentSlots[0] != null
            ? equipmentSlots[0].transform.Find("EquippedMarker")?.GetComponent<Image>()
            : null;

        Transform mainTabRail = FindDescendant(contentRoot, "MainTabs");
        mainTabButtons = mainTabRail != null
            ? mainTabRail.GetComponentsInChildren<Button>(true)
            : new Button[0];

        for (int tabIndex = 0; tabIndex < mainTabButtons.Length; tabIndex++)
        {
            Button tabButton = mainTabButtons[tabIndex];
            if (tabButton == null)
            {
                continue;
            }

            string tabId = GetTabId(tabIndex);
            tabButton.onClick.RemoveAllListeners();
            tabButton.onClick.AddListener(() => SelectMainTab(tabId));
            SetButtonLabel(tabButton, tabId);
        }

        if (pageTitle != null)
        {
            pageTitleGroup = pageTitle.GetComponent<CanvasGroup>();
            if (pageTitleGroup == null)
            {
                pageTitleGroup = pageTitle.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void BindPrompt()
    {
        Transform promptTransform = FindDescendant(contentRoot, "PromptBar");
        promptBackground = promptTransform != null ? promptTransform.GetComponent<Image>() : null;
        promptText = promptTransform != null ? promptTransform.GetComponentInChildren<Text>(true) : null;
        if (promptText != null)
        {
            promptText.supportRichText = true;
            UpdatePrompt(false, false);
        }
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

    private static bool IsPlayStationDevice(InputDevice device)
    {
        string description = $"{device.layout} {device.displayName} {device.description.manufacturer} "
            + $"{device.description.product} {device.description.interfaceName}";
        description = description.ToLowerInvariant();
        return description.Contains("dualshock")
            || description.Contains("dualsense")
            || description.Contains("playstation")
            || description.Contains("ps4")
            || description.Contains("ps5")
            || description.Contains("wireless controller");
    }

    private void UpdatePrompt(bool controller, bool playStation)
    {
        if (promptText == null || (promptInitialized && promptController == controller && promptPlayStation == playStation))
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
        if (promptBackground != null)
        {
            promptBackground.gameObject.SetActive(IsOpen);
        }
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

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform result = FindDescendant(root.GetChild(childIndex), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void RefreshSceneControls()
    {
        visibleWeapons.Clear();
        WeaponLoadout loadout = FindLoadout();
        if (loadout != null && loadout.IsReady)
        {
            for (int weaponIndex = 0; weaponIndex < loadout.Weapons.Count; weaponIndex++)
            {
                WeaponController weapon = loadout.Weapons[weaponIndex];
                if (weapon != null && IsWeaponVisible(weapon) && visibleWeapons.Count < MaxWeaponsPerTab)
                {
                    visibleWeapons.Add(weapon);
                }
            }
        }

        LogInput($"Refresh controls. tab={selectedMainTab} loadout={GetObjectName(loadout?.gameObject)} "
            + $"ready={loadout != null && loadout.IsReady} weapons={loadout?.Weapons.Count ?? 0} "
            + $"slots={equipmentSlots.Length} tabs={mainTabButtons.Length} visible={visibleWeapons.Count}");

        if (pageTitle != null)
        {
            pageTitle.text = "INVENTORY";
        }

        if (detailsText != null)
        {
            detailsText.text = loadout == null || !loadout.IsReady
                ? "LOADOUT UNAVAILABLE"
                : $"FOCUS: D-PAD   /   SELECTION: SUBMIT   /   {loadout.SelectedWheelCount} IN WHEEL";
        }

        for (int slotIndex = 0; slotIndex < equipmentSlots.Length; slotIndex++)
        {
            Button slot = equipmentSlots[slotIndex];
            if (slot == null)
            {
                continue;
            }

            slot.onClick.RemoveAllListeners();
            bool hasWeapon = slotIndex < visibleWeapons.Count;
            slot.gameObject.SetActive(hasWeapon);
            if (!hasWeapon)
            {
                continue;
            }

            WeaponController selectedWeapon = visibleWeapons[slotIndex];
            SetButtonLabel(slot, $"{selectedWeapon.DisplayName}   {selectedWeapon.MagazineAmmo}/{selectedWeapon.ReserveAmmo}");
            ConfigureEquipmentButton(slot);
            SetEquippedMarker(slot, loadout.IsSelectedForWheel(selectedWeapon));
            slot.onClick.AddListener(() => ToggleInventoryWheelSelection(loadout, selectedWeapon));
        }

        RefreshTabVisuals();
        ConfigureControlNavigation();
        UpdateButtonVisuals();
    }

    private static WeaponLoadout FindLoadout()
    {
        WeaponLoadout loadout = FindFirstObjectByType<WeaponLoadout>(FindObjectsInactive.Include);
        if (loadout != null)
        {
            return loadout;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        return player != null ? player.GetComponent<WeaponLoadout>() : null;
    }

    private void SelectMainTab(string tabId)
    {
        selectedMainTab = tabId;
        RefreshSceneControls();
    }

    private void SelectAdjacentMainTab(int direction)
    {
        if (mainTabButtons.Length == 0)
        {
            return;
        }

        int currentIndex = 0;
        for (int tabIndex = 0; tabIndex < mainTabButtons.Length; tabIndex++)
        {
            if (GetTabId(tabIndex) == selectedMainTab)
            {
                currentIndex = tabIndex;
                break;
            }
        }

        int nextIndex = (currentIndex + direction + mainTabButtons.Length) % mainTabButtons.Length;
        SelectMainTab(GetTabId(nextIndex));
    }

    private bool IsWeaponVisible(WeaponController weapon)
    {
        if (selectedMainTab == "PRIMARY")
        {
            return weapon.Category == WeaponCategory.SR
                || weapon.Category == WeaponCategory.AR
                || weapon.Category == WeaponCategory.LMG;
        }

        if (selectedMainTab == "SECONDARY")
        {
            return weapon.Category != WeaponCategory.SR
                && weapon.Category != WeaponCategory.AR
                && weapon.Category != WeaponCategory.LMG;
        }

        return false;
    }

    private void RefreshTabVisuals()
    {
        for (int tabIndex = 0; tabIndex < mainTabButtons.Length; tabIndex++)
        {
            Button tabButton = mainTabButtons[tabIndex];
            ConfigureTabButton(tabButton);
            if (tabButton != null)
            {
                if (tabButton.targetGraphic != null)
                {
                    bool isCurrentTab = GetTabId(tabIndex) == selectedMainTab;
                    tabButton.targetGraphic.color = isCurrentTab
                        ? new Color(1f, 0.34f, 0.04f, 1f)
                        : Color.black;

                    Text tabLabel = tabButton.GetComponentInChildren<Text>(true);
                    if (tabLabel != null)
                    {
                        tabLabel.color = isCurrentTab ? Color.black : Color.white;
                    }
                }

                Navigation navigation = tabButton.navigation;
                navigation.mode = Navigation.Mode.None;
                tabButton.navigation = navigation;
            }
        }
    }

    private void EnsureEquipmentSlots()
    {
        if (equipmentList == null || equipmentSlots.Length >= MaxWeaponsPerTab || equipmentSlots.Length == 0)
        {
            return;
        }

        List<Button> slots = new List<Button>(equipmentSlots);
        Button template = equipmentSlots[0];
        for (int slotIndex = slots.Count; slotIndex < MaxWeaponsPerTab; slotIndex++)
        {
            GameObject clone = Instantiate(template.gameObject, equipmentList, false);
            clone.name = $"OPTION {slotIndex + 1}";
            Button slot = clone.GetComponent<Button>();
            if (slot != null)
            {
                slots.Add(slot);
            }
        }

        equipmentSlots = slots.ToArray();
    }

    private void ToggleInventoryWheelSelection(WeaponLoadout loadout, WeaponController weapon)
    {
        loadout.ToggleWheelSelection(weapon);
        RefreshSceneControls();
    }

    private static void ConfigureEquipmentButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.black;
        colors.highlightedColor = new Color(1f, 0.34f, 0.04f, 1f);
        colors.pressedColor = new Color(0.72f, 0.18f, 0.02f, 1f);
        colors.selectedColor = new Color(1f, 0.34f, 0.04f, 1f);
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = Color.white;
        }
    }

    private static void ConfigureTabButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.black;
        colors.highlightedColor = new Color(1f, 0.34f, 0.04f, 1f);
        colors.pressedColor = new Color(0.72f, 0.18f, 0.02f, 1f);
        colors.selectedColor = new Color(1f, 0.34f, 0.04f, 1f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = Color.white;
        }
    }

    private void UpdateButtonVisuals()
    {
        GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;
        List<Button> slots = GetActiveSlots();
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            Button button = slots[slotIndex];
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = Color.white;
            }

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = button.gameObject == selectedObject
                    ? new Color(1f, 0.34f, 0.04f, 1f)
                    : Color.black;
            }
        }
    }

    private void UpdateTitlePulse()
    {
        if (pageTitle == null)
        {
            return;
        }

        float pulseSpeed = pauseMenu != null ? pauseMenu.TitlePulseSpeed : 0.5f;
        float sawtooth = Mathf.Repeat(Time.unscaledTime * pulseSpeed + titlePulsePhaseOffset, 1f);
        float opacity = Mathf.Lerp(titlePulseMinimumOpacity, 1f, sawtooth);
        if (pageTitleGroup != null)
        {
            pageTitleGroup.alpha = opacity;
        }
    }

    private void LogInputStatus(bool force)
    {
        if (!debugInputLogs || (!force && Time.unscaledTime < nextDebugStatusTime))
        {
            return;
        }

        nextDebugStatusTime = Time.unscaledTime + 2f;
        Vector2 direction = navigateAction != null && navigateAction.enabled
            ? navigateAction.ReadValue<Vector2>()
            : Vector2.zero;
        string eventSystemName = EventSystem.current != null ? EventSystem.current.name : "<none>";
        LogInput($"Status open={IsOpen} contentActive={ContentRoot != null && ContentRoot.activeInHierarchy} "
            + $"eventSystem={eventSystemName} selected={GetSelectedName()} direction={direction} "
            + $"navigate={DescribeAction(navigateAction)} submit={DescribeAction(submitAction)} "
            + $"leftPage={DescribeAction(leftPageAction)} rightPage={DescribeAction(rightPageAction)} "
            + $"tabs={mainTabButtons.Length} activeSlots={GetActiveSlotCount()}");
    }

    private void LogInput(string message)
    {
        if (debugInputLogs)
        {
            Debug.Log($"[InventoryMenu] {message}", this);
        }
    }

    private static string DescribeAction(InputAction action)
    {
        return action == null
            ? "<missing>"
            : $"{action.name}(enabled={action.enabled},bindings={action.bindings.Count})";
    }

    private int GetActiveSlotCount()
    {
        int activeSlotCount = 0;
        for (int slotIndex = 0; slotIndex < equipmentSlots.Length; slotIndex++)
        {
            if (equipmentSlots[slotIndex] != null && equipmentSlots[slotIndex].gameObject.activeInHierarchy)
            {
                activeSlotCount++;
            }
        }

        return activeSlotCount;
    }

    private static string GetSelectedName()
    {
        return GetObjectName(EventSystem.current?.currentSelectedGameObject);
    }

    private static string GetObjectName(GameObject target)
    {
        return target != null ? target.name : "<none>";
    }

    private bool WasLeftPagePressed()
    {
        bool pressed = leftPageAction != null && leftPageAction.WasPressedThisFrame();
        pressed |= Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        pressed |= Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame;
        return pressed;
    }

    private bool WasRightPagePressed()
    {
        bool pressed = rightPageAction != null && rightPageAction.WasPressedThisFrame();
        pressed |= Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        pressed |= Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame;
        return pressed;
    }

    private void ConfigureControlNavigation()
    {
        List<Button> slots = GetActiveSlots();
        if (slots.Count == 0)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            Button button = slots[slotIndex];
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = true;
            navigation.selectOnUp = slots[(slotIndex + slots.Count - 1) % slots.Count];
            navigation.selectOnDown = slots[(slotIndex + 1) % slots.Count];
            navigation.selectOnLeft = slots[(slotIndex + slots.Count - 1) % slots.Count];
            navigation.selectOnRight = slots[(slotIndex + 1) % slots.Count];
            button.navigation = navigation;
        }
    }

    private List<Button> GetActiveSlots()
    {
        List<Button> slots = new List<Button>();
        for (int slotIndex = 0; slotIndex < equipmentSlots.Length; slotIndex++)
        {
            if (equipmentSlots[slotIndex] != null && equipmentSlots[slotIndex].gameObject.activeSelf)
            {
                slots.Add(equipmentSlots[slotIndex]);
            }
        }

        return slots;
    }

    private void SetEquippedMarker(Button button, bool visible)
    {
        if (!equippedMarkers.TryGetValue(button, out Image marker) || marker == null)
        {
            marker = button.transform.Find("EquippedMarker")?.GetComponent<Image>();
            if (marker == null && equippedMarkerTemplate != null && button != equippedMarkerTemplate.GetComponentInParent<Button>())
            {
                GameObject markerObject = Instantiate(equippedMarkerTemplate.gameObject, button.transform, false);
                markerObject.name = "EquippedMarker";
                marker = markerObject.GetComponent<Image>();
            }

            if (marker == null)
            {
                return;
            }

            equippedMarkers[button] = marker;
        }

        marker.gameObject.SetActive(visible);
    }

    private static string GetTabId(int tabIndex)
    {
        string[] tabIds = { "OTHER", "EQUIPMENT", "ITEMS", "SECONDARY", "PRIMARY" };
        return tabIndex >= 0 && tabIndex < tabIds.Length ? tabIds[tabIndex] : "OTHER";
    }

    private Button GetFirstActiveSlot()
    {
        for (int slotIndex = 0; slotIndex < equipmentSlots.Length; slotIndex++)
        {
            if (equipmentSlots[slotIndex] != null && equipmentSlots[slotIndex].gameObject.activeSelf)
            {
                return equipmentSlots[slotIndex];
            }
        }

        return null;
    }

    private Button GetFirstActiveTab()
    {
        for (int tabIndex = 0; tabIndex < mainTabButtons.Length; tabIndex++)
        {
            if (mainTabButtons[tabIndex] != null && mainTabButtons[tabIndex].gameObject.activeSelf)
            {
                return mainTabButtons[tabIndex];
            }
        }

        return null;
    }

    private Text FindText(string expectedText)
    {
        Text[] texts = contentRoot.GetComponentsInChildren<Text>(true);
        for (int textIndex = 0; textIndex < texts.Length; textIndex++)
        {
            if (texts[textIndex].text == expectedText)
            {
                return texts[textIndex];
            }
        }

        return null;
    }

    private static void SetButtonLabel(Button button, string labelText)
    {
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = labelText;
        }
    }

}
