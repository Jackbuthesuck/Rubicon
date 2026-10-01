using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class WeaponModelEntry
{
    public string modelId;
    public GameObject prefab;
}

public sealed class WeaponLoadout : MonoBehaviour
{
    [SerializeField] private WeaponController[] startingWeapons;
    [SerializeField] private GameObject[] weaponPrefabs;
    [SerializeField] private WeaponModelEntry[] weaponModels;
    [SerializeField] private GameObject fallbackWeaponModel;
    [SerializeField, Min(0)] private int startingSlot;

    private readonly List<WeaponController> weapons = new List<WeaponController>();
    private int activeSlot = -1;

    public IReadOnlyList<WeaponController> Weapons => weapons;
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
            SelectWeapon(Mathf.Clamp(startingSlot, 0, weapons.Count - 1));
        }

        IsReady = true;
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
                weapon.SetEquipped(weaponIndex == activeSlot);
            }
        }

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
            if (weapon != null && weapon.Category == category)
            {
                matchingWeapons.Add(weapon);
            }
        }

        return matchingWeapons;
    }

    private void CollectExistingWeapons()
    {
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
            AddWeapon(weapon);
        }
    }

    private void CreateConfiguredWeapons()
    {
        if (weaponPrefabs == null)
        {
            return;
        }

        foreach (GameObject weaponPrefab in weaponPrefabs)
        {
            if (weaponPrefab == null)
            {
                continue;
            }

            WeaponController weapon = weaponPrefab.GetComponentInChildren<WeaponController>(true);
            if (weapon == null)
            {
                continue;
            }

            WeaponController instance = Instantiate(weaponPrefab, transform).GetComponentInChildren<WeaponController>(true);
            AddWeapon(instance);
        }
    }

    private void AddWeapon(WeaponController weapon)
    {
        if (weapon != null && !weapons.Contains(weapon))
        {
            weapons.Add(weapon);
        }
    }
}