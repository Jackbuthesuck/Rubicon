using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class WeaponRadialMenu : MonoBehaviour
{
    private const int MaxWeaponsPerWheel = 8;
    [Header("References")]
    [SerializeField] private WeaponLoadout loadout;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Font radialFont;

    [Header("Radial Layout")]
    [SerializeField, Min(40f)] private float radius = 180f;
    [SerializeField, Min(24f)] private float buttonSize = 76f;
    [SerializeField] private Color panelColor = new Color(0.02f, 0.025f, 0.02f, 0.94f);
    [SerializeField] private Color buttonColor = Color.black;
    [SerializeField] private Color selectedColor = Color.red;

    private readonly List<Button> primaryButtons = new List<Button>();
    private readonly List<Button> secondaryButtons = new List<Button>();
    private readonly List<WeaponController> primaryWeapons = new List<WeaponController>();
    private readonly List<WeaponController> secondaryWeapons = new List<WeaponController>();
    private InputAction primaryWheelAction;
    private InputAction secondaryWheelAction;
    private InputAction lookAction;
    private GameObject primaryMenuRoot;
    private GameObject secondaryMenuRoot;
    private Font font;
    private Vector2 selectionDirection = Vector2.zero;
    private bool wheelWasPressed;
    private bool pendingPrimaryWheel;
    private int pendingSelectionSlot = -1;
    private bool menuBuilt;

    private void Awake()
    {
        if (loadout == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                loadout = player.GetComponent<WeaponLoadout>();
                if (loadout == null)
                {
                    loadout = player.gameObject.AddComponent<WeaponLoadout>();
                }
            }
        }

        if (inputActions != null)
        {
            primaryWheelAction = inputActions.FindAction("Player/Primary Wheel", false);
            secondaryWheelAction = inputActions.FindAction("Player/Secondary Wheel", false);
            lookAction = inputActions.FindAction("Player/Look", false);
            primaryWheelAction?.Enable();
            secondaryWheelAction?.Enable();
            lookAction?.Enable();
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        font = radialFont;
    }

    private void Start()
    {
        primaryWheelAction?.Enable();
        secondaryWheelAction?.Enable();
        lookAction?.Enable();
        TryBuildMenu();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (loadout != null)
            {
                loadout.WeaponSelected += HandleWeaponSelected;
                loadout.WheelSelectionChanged += HandleWheelSelectionChanged;
            }
            primaryWheelAction?.Enable();
            secondaryWheelAction?.Enable();
            lookAction?.Enable();
        }
    }

    private void OnDisable()
    {
        if (loadout != null)
        {
            loadout.WeaponSelected -= HandleWeaponSelected;
            loadout.WheelSelectionChanged -= HandleWheelSelectionChanged;
        }
        primaryWheelAction?.Disable();
        secondaryWheelAction?.Disable();
        lookAction?.Disable();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            wheelWasPressed = false;
            SetMenuVisible(false, false);
            return;
        }

        if (!menuBuilt)
        {
            TryBuildMenu();
        }

        bool primaryPressed = primaryWheelAction != null && primaryWheelAction.IsPressed();
        bool secondaryPressed = secondaryWheelAction != null && secondaryWheelAction.IsPressed();
        if (Keyboard.current != null)
        {
            primaryPressed |= Keyboard.current.digit1Key.isPressed;
            secondaryPressed |= Keyboard.current.digit2Key.isPressed;
        }
        if (Gamepad.current != null)
        {
            primaryPressed |= Gamepad.current.dpad.up.isPressed;
            secondaryPressed |= Gamepad.current.dpad.down.isPressed;
        }
        bool wheelPressed = primaryPressed || secondaryPressed;
        if (wheelPressed && !wheelWasPressed)
        {
            selectionDirection = Vector2.zero;
            pendingSelectionSlot = -1;
        }

        SetMenuVisible(primaryPressed, secondaryPressed);

        if (!wheelPressed && wheelWasPressed)
        {
            if (pendingSelectionSlot >= 0 && loadout != null)
            {
                SelectWheelWeapon(pendingPrimaryWheel, pendingSelectionSlot);
            }

            pendingSelectionSlot = -1;
        }

        wheelWasPressed = wheelPressed;

        if (!wheelPressed || loadout == null)
        {
            return;
        }

        Vector2 direction = ReadSelectionDirection();
        if (direction.sqrMagnitude > 0.01f)
        {
            List<Button> visibleButtons = primaryPressed ? primaryButtons : secondaryButtons;
            if (visibleButtons.Count == 0)
            {
                return;
            }

            int slot = GetSlotFromDirection(direction, visibleButtons.Count);
            pendingPrimaryWheel = primaryPressed;
            pendingSelectionSlot = slot;
            RefreshSelection(primaryButtons, primaryWeapons);
            RefreshSelection(secondaryButtons, secondaryWeapons);
            HighlightPendingSelection();
        }
    }

    private void TryBuildMenu()
    {
        if (menuBuilt)
        {
            return;
        }

        if (loadout == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                loadout = player.GetComponent<WeaponLoadout>();
                if (loadout == null)
                {
                    loadout = player.gameObject.AddComponent<WeaponLoadout>();
                }
            }
        }

        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (canvas == null || loadout == null || !loadout.IsReady)
        {
            return;
        }

        BuildMenu(WeaponCategory.SR, WeaponCategory.AR, WeaponCategory.LMG, out primaryMenuRoot, primaryButtons, primaryWeapons);
        BuildMenu(WeaponCategory.HG, WeaponCategory.SG, WeaponCategory.SMG, out secondaryMenuRoot, secondaryButtons, secondaryWeapons);
        SetMenuVisible(false, false);
        menuBuilt = true;
    }

    private void BuildMenu(WeaponCategory firstCategory, WeaponCategory secondCategory, WeaponCategory thirdCategory, out GameObject menuRoot, List<Button> buttons, List<WeaponController> menuWeapons)
    {
        menuRoot = null;
        if (canvas == null || loadout == null)
        {
            return;
        }

        menuRoot = new GameObject($"{firstCategory}RadialMenu", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        menuRoot.transform.SetParent(canvas.transform, false);
        menuRoot.transform.SetAsLastSibling();
        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(radius * 2f + buttonSize, radius * 2f + buttonSize);
        menuRoot.GetComponent<Image>().color = panelColor;

        List<WeaponController> weapons = new List<WeaponController>();
        weapons.AddRange(loadout.GetWeapons(firstCategory));
        weapons.AddRange(loadout.GetWeapons(secondCategory));
        weapons.AddRange(loadout.GetWeapons(thirdCategory));
        int weaponCount = Mathf.Min(weapons.Count, MaxWeaponsPerWheel);
        for (int weaponIndex = 0; weaponIndex < weaponCount; weaponIndex++)
        {
            CreateButton(rootRect, weaponIndex, weapons[weaponIndex], buttons, menuWeapons, weaponCount);
        }

        RefreshSelection(buttons, menuWeapons);
    }

    private void CreateButton(RectTransform parent, int slot, WeaponController weapon, List<Button> buttons, List<WeaponController> menuWeapons, int buttonCount)
    {
        GameObject buttonObject = new GameObject($"WeaponSlot{slot + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        float angle = 90f - slot * 360f / Mathf.Max(1, buttonCount);
        float radians = angle * Mathf.Deg2Rad;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
        buttonRect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;

        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;
        Button button = buttonObject.GetComponent<Button>();
        WeaponController selectedWeapon = weapon;
        button.onClick.AddListener(() => loadout.SelectWeapon(selectedWeapon));
        buttons.Add(button);
        menuWeapons.Add(weapon);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Text label = labelObject.GetComponent<Text>();
        label.font = font;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 8;
        label.resizeTextMaxSize = 16;
        label.text = weapon != null ? weapon.DisplayName : "EMPTY";
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 4f);
        labelRect.offsetMax = new Vector2(-4f, -4f);
    }

    private Vector2 ReadSelectionDirection()
    {
        Vector2 gamepadDirection = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
        if (gamepadDirection.sqrMagnitude > 0.01f)
        {
            return gamepadDirection;
        }

        selectionDirection += (lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero) * 0.02f;
        return Vector2.ClampMagnitude(selectionDirection, 1f);
    }

    private int GetSlotFromDirection(Vector2 direction, int buttonCount)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float step = 360f / buttonCount;
        return Mathf.FloorToInt((90f - angle + step * 0.5f + 360f) % 360f / step);
    }

    private void SelectWheelWeapon(bool primaryWheel, int slot)
    {
        WeaponCategory[] categories = primaryWheel
            ? new[] { WeaponCategory.SR, WeaponCategory.AR, WeaponCategory.LMG }
            : new[] { WeaponCategory.HG, WeaponCategory.SG, WeaponCategory.SMG };
        List<WeaponController> weapons = GetRadialWeapons(categories);

        if (weapons.Count > 0)
        {
            loadout.SelectWeapon(weapons[Mathf.Clamp(slot, 0, weapons.Count - 1)]);
        }
    }

    private void RefreshSelection(List<Button> buttons, List<WeaponController> menuWeapons)
    {
        for (int buttonIndex = 0; buttonIndex < buttons.Count; buttonIndex++)
        {
            buttons[buttonIndex].GetComponent<Image>().color = loadout.ActiveWeapon == menuWeapons[buttonIndex]
                ? selectedColor
                : buttonColor;
        }
    }

    private void HighlightPendingSelection()
    {
        List<Button> buttons = pendingPrimaryWheel ? primaryButtons : secondaryButtons;
        if (pendingSelectionSlot < 0 || pendingSelectionSlot >= buttons.Count)
        {
            return;
        }

        buttons[pendingSelectionSlot].GetComponent<Image>().color = selectedColor;
    }

    private List<WeaponController> GetRadialWeapons(WeaponCategory[] categories)
    {
        List<WeaponController> weapons = new List<WeaponController>();
        foreach (WeaponCategory category in categories)
        {
            foreach (WeaponController weapon in loadout.GetWeapons(category))
            {
                if (weapons.Count >= MaxWeaponsPerWheel)
                {
                    return weapons;
                }

                weapons.Add(weapon);
            }
        }

        return weapons;
    }

    private void HandleWeaponSelected(WeaponController weapon)
    {
        RefreshSelection(primaryButtons, primaryWeapons);
        RefreshSelection(secondaryButtons, secondaryWeapons);
    }

    private void DestroyMenuRoots()
    {
        if (primaryMenuRoot != null)
        {
            Destroy(primaryMenuRoot);
        }

        if (secondaryMenuRoot != null)
        {
            Destroy(secondaryMenuRoot);
        }

        primaryMenuRoot = null;
        secondaryMenuRoot = null;
        primaryButtons.Clear();
        secondaryButtons.Clear();
        primaryWeapons.Clear();
        secondaryWeapons.Clear();
    }

    private void HandleWheelSelectionChanged()
    {
        DestroyMenuRoots();
        menuBuilt = false;
    }

    private void SetMenuVisible(bool primaryVisible, bool secondaryVisible)
    {
        SetMenuRootVisible(primaryMenuRoot, primaryVisible);
        SetMenuRootVisible(secondaryMenuRoot, secondaryVisible);
    }

    private static void SetMenuRootVisible(GameObject menuRoot, bool visible)
    {
        if (menuRoot == null)
        {
            return;
        }

        CanvasGroup group = menuRoot.GetComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}