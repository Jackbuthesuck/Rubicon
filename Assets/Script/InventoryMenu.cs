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

    private readonly List<WeaponController> visibleWeapons = new List<WeaponController>();
    private readonly Dictionary<Button, Image> equippedMarkers = new Dictionary<Button, Image>();
    private string selectedMainTab = "PRIMARY";
    private InputAction leftPageAction;
    private InputAction rightPageAction;
    private InputAction navigateAction;
    private InputAction submitAction;
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
        Close();
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
        UpdateButtonVisuals();

        if (WasLeftPagePressed())
        {
            LogInput($"L Page pressed. selectedBefore={GetSelectedName()}");
            SelectAdjacentMainTab(-1);
            LogInput($"L Page selectedAfter={GetSelectedName()}");
        }
        else if (WasRightPagePressed())
        {
            LogInput($"R Page pressed. selectedBefore={GetSelectedName()}");
            SelectAdjacentMainTab(1);
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
            SetButtonLabel(slot, $"{GetCategoryLabel(selectedWeapon.Category)}   {selectedWeapon.DisplayName}   {selectedWeapon.MagazineAmmo}/{selectedWeapon.ReserveAmmo}");
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
                    tabButton.targetGraphic.color = GetTabId(tabIndex) == selectedMainTab
                        ? new Color(1f, 0.34f, 0.04f, 1f)
                        : Color.black;
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
            GameObject markerObject = new GameObject("EquippedMarker", typeof(RectTransform), typeof(Image));
            markerObject.transform.SetParent(button.transform, false);
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0f, 0f);
            markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = new Vector2(0f, 0.5f);
            markerRect.anchoredPosition = new Vector2(8f, 0f);
            markerRect.sizeDelta = new Vector2(6f, -16f);
            marker = markerObject.GetComponent<Image>();
            marker.color = Color.white;
            marker.raycastTarget = false;
            equippedMarkers[button] = marker;
        }

        marker.gameObject.SetActive(visible);
    }

    private static string GetTabId(int tabIndex)
    {
        string[] tabIds = { "PRIMARY", "SECONDARY", "ITEMS", "EQUIPMENT", "OTHER" };
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

    private static string GetCategoryLabel(WeaponCategory category)
    {
        return category == WeaponCategory.SR || category == WeaponCategory.AR || category == WeaponCategory.LMG
            ? "PRIMARY"
            : "SECONDARY";
    }
}
