using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class WeaponDefinition
{
    public string id = "pistol";
    public string displayName = "PISTOL";
    public int magazineCapacity = 12;
    public int startingMagazineAmmo = 12;
    public int startingReserveAmmo = 120;
    public float roundsPerMinute = 600f;
    public float reloadDuration = 1.2f;
    public bool automatic;
    public bool isTopLoad;
}

[RequireComponent(typeof(Magazine))]
public sealed class WeaponController : MonoBehaviour
{
    [Header("Weapon Data")]
    [SerializeField] private string weaponId = "pistol";
    [SerializeField] private TextAsset fallbackDefinition;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Shot")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private float shotDistance = 100f;
    [SerializeField] private LayerMask shotMask = ~0;

    private Magazine magazine;
    private InputAction shootAction;
    private InputAction reloadAction;
    private WeaponDefinition definition;
    private float nextShotTime;
    private float reloadUntil;

    public string DisplayName => definition != null ? definition.displayName : weaponId;
    public int MagazineAmmo => magazine != null ? magazine.CurrentAmmo : 0;
    public int MagazineCapacity => magazine != null ? magazine.Capacity : 1;
    public int ReserveAmmo => magazine != null ? magazine.ReserveAmmo : 0;
    public int ChamberAmmo => magazine != null && magazine.Chambered ? 1 : 0;
    public bool TopLoad => definition != null && definition.isTopLoad;
    public bool IsReloading => reloadUntil > Time.time;

    private void Awake()
    {
        magazine = GetComponent<Magazine>();
        definition = LoadDefinition();
        magazine.Configure(
            definition.magazineCapacity,
            definition.startingMagazineAmmo,
            definition.startingReserveAmmo,
            true);

        if (inputActions != null)
        {
            shootAction = inputActions.FindAction("Player/Shoot", false);
            reloadAction = inputActions.FindAction("Player/Reload", false);
        }
    }

    private void OnEnable()
    {
        shootAction?.Enable();
        reloadAction?.Enable();
    }

    private void OnDisable()
    {
        shootAction?.Disable();
        reloadAction?.Disable();
    }

    private void Update()
    {
        bool reloadPressed = reloadAction != null && reloadAction.WasPressedThisFrame();
        if (reloadAction == null && Keyboard.current != null)
        {
            reloadPressed = Keyboard.current.rKey.wasPressedThisFrame;
        }

        if (reloadPressed)
        {
            BeginReload();
        }

        if (IsReloading)
        {
            if (Time.time >= reloadUntil)
            {
                bool loaded = magazine.Reload(definition.isTopLoad);
                if (definition.isTopLoad && loaded && magazine.CurrentAmmo < magazine.Capacity && magazine.ReserveAmmo > 0)
                {
                    reloadUntil = Time.time + Mathf.Max(0.05f, definition.reloadDuration);
                }
                else
                {
                    reloadUntil = 0f;
                }
            }

            return;
        }

        if (shootAction == null)
        {
            return;
        }

        bool shouldShoot = definition.automatic
            ? shootAction.IsPressed()
            : shootAction.WasPressedThisFrame();
        if (shouldShoot)
        {
            TryShoot();
        }
    }

    private void TryShoot()
    {
        if (Time.time < nextShotTime || !magazine.ConsumeShot())
        {
            return;
        }

        nextShotTime = Time.time + 60f / Mathf.Max(1f, definition.roundsPerMinute);
        Transform shotOrigin = muzzle != null ? muzzle : transform;
        Ray shotRay = new Ray(shotOrigin.position, shotOrigin.forward);
        if (Physics.Raycast(shotRay, out RaycastHit hit, shotDistance, shotMask, QueryTriggerInteraction.Ignore))
        {
            Debug.DrawLine(shotRay.origin, hit.point, Color.red, 0.2f);
        }
        else
        {
            Debug.DrawRay(shotRay.origin, shotRay.direction * shotDistance, Color.red, 0.2f);
        }
    }

    private void BeginReload()
    {
        if (IsReloading || magazine.CurrentAmmo >= magazine.Capacity || magazine.ReserveAmmo <= 0)
        {
            return;
        }

        reloadUntil = Time.time + Mathf.Max(0.05f, definition.reloadDuration);
    }

    private WeaponDefinition LoadDefinition()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Weapons", weaponId + ".json");
        if (File.Exists(path))
        {
            WeaponDefinition loaded = JsonUtility.FromJson<WeaponDefinition>(File.ReadAllText(path));
            if (loaded != null)
            {
                return loaded;
            }
        }

        if (fallbackDefinition != null)
        {
            WeaponDefinition loaded = JsonUtility.FromJson<WeaponDefinition>(fallbackDefinition.text);
            if (loaded != null)
            {
                return loaded;
            }
        }

        return new WeaponDefinition { id = weaponId };
    }
}
