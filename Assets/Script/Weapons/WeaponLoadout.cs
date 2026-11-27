using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public sealed class WeaponModelEntry
{
    public string modelId;
    public GameObject prefab;
}

public sealed class WeaponLoadout : MonoBehaviour
{
    [SerializeField] private WeaponController[] startingWeapons;
    [SerializeField] private WeaponController[] weaponPrefabs;
    [SerializeField] private WeaponModelEntry[] weaponModels;
    [SerializeField] private GameObject fallbackWeaponModel;
    [SerializeField, Min(0)] private int startingSlot;

    private readonly List<WeaponController> weapons = new List<WeaponController>();
    private readonly Dictionary<WeaponController, GameObject> weaponRoots = new Dictionary<WeaponController, GameObject>();
    private readonly HashSet<WeaponController> wheelSelection = new HashSet<WeaponController>();
    private int activeSlot = -1;

    public event System.Action<WeaponController> WeaponSelected;
    public event System.Action WheelSelectionChanged;

    public IReadOnlyList<WeaponController> Weapons => weapons;
    public int SelectedWheelCount => wheelSelection.Count;
    public int ActiveSlot => activeSlot;
    public WeaponController ActiveWeapon => activeSlot >= 0 && activeSlot < weapons.Count ? weapons[activeSlot] : null;
    public bool IsReady { get; private set; }

    public GameObject ResolveModel(string modelId)
    {
        if (weaponModels != null)
        {
            foreach (WeaponModelEntry model in weaponModels)
            {
                if (model != null && model.prefab != null && string.Equals(model.modelId, modelId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return model.prefab;
                }
            }
        }

        return fallbackWeaponModel;
    }

    private void Awake()
    {
        CollectExistingWeapons();
        CreateConfiguredWeapons();

        if (weapons.Count > 0)
        {
            for (int weaponIndex = 0; weaponIndex < weapons.Count; weaponIndex++)
            {
                wheelSelection.Add(weapons[weaponIndex]);
            }

            SelectWeapon(Mathf.Clamp(startingSlot, 0, weapons.Count - 1));
        }

        IsReady = true;
        Debug.Log($"WeaponLoadout ready: {weapons.Count} weapons [{string.Join(", ", weapons.ConvertAll(weapon => weapon != null ? weapon.WeaponId : "null"))}].", this);
    }

    private void Update()
    {
        // Weapon selection is controlled by the Input System wheels/loadout UI,
        // not by direct number-key slot shortcuts.
    }

    public bool SelectWeapon(int slot)
    {
        if (slot < 0 || slot >= weapons.Count || weapons[slot] == null)
        {
            return false;
        }

        activeSlot = slot;
        for (int weaponIndex = 0; weaponIndex < weapons.Count; weaponIndex++)
        {
            WeaponController weapon = weapons[weaponIndex];
            if (weapon != null)
            {
                bool selected = weaponIndex == activeSlot;
                weapon.SetEquipped(selected);
                if (weaponRoots.TryGetValue(weapon, out GameObject weaponRoot) && weaponRoot != null)
                {
                    weaponRoot.SetActive(selected);
                }
            }
        }

        WeaponSelected?.Invoke(ActiveWeapon);

        return true;
    }

    public bool SelectWeapon(WeaponController weapon)
    {
        int slot = weapons.IndexOf(weapon);
        return SelectWeapon(slot);
    }

    public List<WeaponController> GetWeapons(WeaponCategory category)
    {
        List<WeaponController> matchingWeapons = new List<WeaponController>();
        foreach (WeaponController weapon in weapons)
        {
            if (weapon != null && weapon.Category == category && IsSelectedForWheel(weapon))
            {
                matchingWeapons.Add(weapon);
            }
        }

        return matchingWeapons;
    }

    public bool IsSelectedForWheel(WeaponController weapon)
    {
        return weapon != null && wheelSelection.Contains(weapon);
    }

    public void ToggleWheelSelection(WeaponController weapon)
    {
        if (weapon == null || !weapons.Contains(weapon))
        {
            return;
        }

        if (!wheelSelection.Add(weapon))
        {
            wheelSelection.Remove(weapon);
        }

        WheelSelectionChanged?.Invoke();
    }

    private void CollectExistingWeapons()
    {
        if (HasConfiguredWeaponPrefabs())
        {
            WeaponController[] configuredChildWeapons = GetComponentsInChildren<WeaponController>(true);
            foreach (WeaponController weapon in configuredChildWeapons)
            {
                weapon.SetEquipped(false);
            }

            return;
        }

        if (startingWeapons != null && startingWeapons.Length > 0)
        {
            foreach (WeaponController weapon in startingWeapons)
            {
                AddWeapon(weapon);
            }

            return;
        }

        WeaponController[] childWeapons = GetComponentsInChildren<WeaponController>(true);
        foreach (WeaponController weapon in childWeapons)
        {
            if (weapon != GetComponent<WeaponController>())
            {
                AddWeapon(weapon);
            }
        }
    }

    private bool HasConfiguredWeaponPrefabs()
    {
        if (weaponPrefabs == null)
        {
            return false;
        }

        foreach (WeaponController weaponPrefab in weaponPrefabs)
        {
            if (weaponPrefab != null)
            {
                return true;
            }
        }

        return false;
    }

    private void CreateConfiguredWeapons()
    {
        if (weaponPrefabs == null)
        {
            return;
        }

        foreach (WeaponController weaponPrefab in weaponPrefabs)
        {
            if (weaponPrefab == null)
            {
                continue;
            }

            try
            {
                GameObject instanceObject = Instantiate(weaponPrefab.transform.root.gameObject, transform);
                instanceObject.transform.localPosition = new Vector3(0.6f, 0f, 0f);
                WeaponController instance = instanceObject.GetComponentInChildren<WeaponController>(true);
                if (instance == null)
                {
                    Debug.LogError($"Weapon prefab '{weaponPrefab.name}' did not produce a WeaponController.", weaponPrefab);
                    Destroy(instanceObject);
                    continue;
                }

                instanceObject.SetActive(false);
                AddWeapon(instance, instanceObject);
            }
            catch (MissingReferenceException)
            {
                continue;
            }
        }
    }

    private void AddWeapon(WeaponController weapon)
    {
        AddWeapon(weapon, weapon != null ? weapon.transform.root.gameObject : null);
    }

    private void AddWeapon(WeaponController weapon, GameObject weaponRoot)
    {
        if (weapon != null && !weapons.Contains(weapon))
        {
            weapons.Add(weapon);
            if (weaponRoot != null && weaponRoot != gameObject)
            {
                weaponRoots[weapon] = weaponRoot;
                weaponRoot.transform.localPosition = new Vector3(0.6f, 0f, 0f);
            }
        }
    }
}