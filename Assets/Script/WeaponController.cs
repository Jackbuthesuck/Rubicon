using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponCategory
{
    SR,
    AR,
    LMG,
    HG,
    SG,
    SMG
}

[Serializable]
public sealed class WeaponDefinition
{
    public string id = "pistol";
    public string displayName = "PISTOL";
    public string ammoId = "9x19";
    public string modelId = "ks23";
    public int magazineCapacity = 12;
    public int startingMagazineAmmo = 12;
    public int startingReserveAmmo = 120;
    public float roundsPerMinute = 600f;
    public float reloadDuration = 1.2f;
    public bool automatic;
    public bool isTopLoad;
    public string category = "HG";
}

[Serializable]
public sealed class AmmoDefinition
{
    public string id = "9x19";
    public string displayName = "9x19 NATO 124gr FMJ";
    public int damage = 20;
    public int pellets = 1;
    public float moa = 6f;
    public float projectileSpeed = 900f;
    public float recoil = 2f;
    public int penetration = 100;
    public string vpamLevel = "PM1";
    public string penetrationRangeMeters = "0-25";
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
    private AmmoDefinition ammoDefinition;
    private PlayerController playerController;
    private float nextShotTime;
    private float reloadUntil;
    private bool equipped = true;
    private Transform visualModel;

    public string DisplayName => definition != null ? definition.displayName : weaponId;
    public string WeaponId => definition != null ? definition.id : weaponId;
    public string ModelId => definition != null ? definition.modelId : "ks23";
    public WeaponCategory Category
    {
        get
        {
            return definition != null && System.Enum.TryParse(definition.category, true, out WeaponCategory category)
                ? category
                : WeaponCategory.HG;
        }
    }
    public int MagazineAmmo => magazine != null ? magazine.CurrentAmmo : 0;
    public int MagazineCapacity => magazine != null ? magazine.Capacity : 1;
    public int ReserveAmmo => magazine != null ? magazine.ReserveAmmo : 0;
    public int ChamberAmmo => magazine != null && magazine.Chambered ? 1 : 0;
    public bool TopLoad => definition != null && definition.isTopLoad;
    public string AmmoId => ammoDefinition != null ? ammoDefinition.id : string.Empty;
    public AmmoDefinition Ammo => ammoDefinition;
    public bool IsReloading => reloadUntil > Time.time;

    public void SetEquipped(bool isEquipped)
    {
        equipped = isEquipped;
    }

    private void Awake()
    {
        magazine = GetComponent<Magazine>();
        playerController = GetComponentInParent<PlayerController>();
        definition = LoadDefinition();
        ammoDefinition = LoadAmmoDefinition(definition.ammoId);
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

        if (muzzle == null)
        {
            muzzle = FindBarrel(transform);
        }

        visualModel = FindVisualModel(transform, playerController);
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
        if (!equipped)
        {
            return;
        }

        UpdateAimModel();

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
        if (playerController != null && !playerController.IsAiming)
        {
            return;
        }

        if (Time.time < nextShotTime || !magazine.ConsumeShot())
        {
            return;
        }

        nextShotTime = Time.time + 60f / Mathf.Max(1f, definition.roundsPerMinute);
        Transform shotOrigin = muzzle != null ? muzzle : transform;
        Ray cameraRay = GetCameraCenterRay(shotOrigin.position);
        Ray shotRay = new Ray(shotOrigin.position, cameraRay.direction);
        if (Physics.Raycast(cameraRay, out RaycastHit aimHit, shotDistance, shotMask, QueryTriggerInteraction.Ignore))
        {
            shotRay.direction = (aimHit.point - shotOrigin.position).normalized;
        }
        if (Physics.Raycast(shotRay, out RaycastHit hit, shotDistance, shotMask, QueryTriggerInteraction.Ignore))
        {
            DrawTracer(shotRay.origin, hit.point);
        }
        else
        {
            DrawTracer(shotRay.origin, shotRay.origin + shotRay.direction * shotDistance);
        }
    }

    private void DrawTracer(Vector3 start, Vector3 end)
    {
        GameObject tracerObject = new GameObject("BulletTracer");
        LineRenderer tracer = tracerObject.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        tracer.startWidth = 0.018f;
        tracer.endWidth = 0.004f;
        tracer.startColor = Color.yellow;
        tracer.endColor = new Color(1f, 0.7f, 0.1f, 0f);
        tracer.material = new Material(Shader.Find("Sprites/Default"));
        Destroy(tracer.material, 0.06f);
        Destroy(tracerObject, 0.06f);
    }

    private static Transform FindBarrel(Transform weaponRoot)
    {
        foreach (Transform child in weaponRoot.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, "Barrel", StringComparison.OrdinalIgnoreCase)
                || child.name.IndexOf("Barrel", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
        }

        return weaponRoot;
    }

    private void UpdateAimModel()
    {
        if (visualModel == null || playerController == null || !playerController.IsAiming)
        {
            return;
        }

        Ray cameraRay = GetCameraCenterRay(visualModel.position);
        Quaternion targetRotation = Quaternion.LookRotation(cameraRay.direction, Vector3.up);
        visualModel.rotation = Quaternion.Slerp(visualModel.rotation, targetRotation, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }

    private static Ray GetCameraCenterRay(Vector3 origin)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return new Ray(origin, Vector3.forward);
        }

        return camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    private static Transform FindVisualModel(Transform weaponRoot, PlayerController owner)
    {
        if (weaponRoot.GetComponent<Renderer>() != null)
        {
            return owner != null && weaponRoot == owner.transform ? null : weaponRoot;
        }

        foreach (Renderer renderer in weaponRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform != owner?.transform)
            {
                return renderer.transform;
            }
        }

        return null;
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

    private static AmmoDefinition LoadAmmoDefinition(string ammoId)
    {
        if (string.IsNullOrWhiteSpace(ammoId))
        {
            return null;
        }

        string path = Path.Combine(Application.streamingAssetsPath, "Ammo", ammoId + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonUtility.FromJson<AmmoDefinition>(File.ReadAllText(path));
    }
}
