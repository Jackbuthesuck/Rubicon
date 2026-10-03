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
    public float reloadChamberDuration = 0.25f;
    public float semiAutoCycleDuration = 0.25f;
    public float doubleActionHoldDuration = 0.2f;
    public float aimRecoilYawMultiplier = 1f;
    public float aimRecoilRandomness = 0.35f;
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
    [SerializeField, Min(0f)] private float aimRecoilPitchMultiplier = 1f;
    [SerializeField, Min(0f)] private float aimRecoilYawMultiplier = 1f;
    [SerializeField, Min(0f)] private float aimRecoilMaximum = 8f;
    [SerializeField, Min(0f)] private float aimRecoilRecoverySpeed = 20f;
    [SerializeField, Min(0.001f)] private float aimRecoilSmoothTime = 0.045f;
    [SerializeField, Min(0f)] private float aimRecoilPatternResetDelay = 0.25f;

    private Magazine magazine;
    private InputAction shootAction;
    private InputAction reloadAction;
    private WeaponDefinition definition;
    private AmmoDefinition ammoDefinition;
    private PlayerController playerController;
    private float nextShotTime;
    private float chamberStartedAt;
    private float chamberUntil;
    private float reloadUntil;
    private float reloadStartedAt;
    private float reloadChamberUntil;
    private float reloadChamberStartedAt;
    private bool equipped = true;
    private float doubleActionTriggerStartedAt;
    private bool doubleActionTriggerHeld;
    private bool doubleActionTriggerCommitted;
    private bool hammerCocked = true;
    private float aimRecoilPitchOffset;
    private float aimRecoilYawOffset;
    private float aimRecoilPitchTarget;
    private float aimRecoilYawTarget;
    private float aimRecoilPitchVelocity;
    private float aimRecoilYawVelocity;
    private float lastAimRecoilShotTime = float.NegativeInfinity;
    private float aimRecoilWavePhase;
    private Quaternion baseLocalRotation;
    private readonly RaycastHit[] raycastBuffer = new RaycastHit[32];

    public event Action<float> ShotFired;
    public Vector2 AimRecoilOffset => new Vector2(aimRecoilYawOffset, aimRecoilPitchOffset);

    public string DisplayName
    {
        get
        {
            string displayName = definition != null ? definition.displayName : weaponId;
            if (!string.Equals(WeaponId, "ks23", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(WeaponId, "rsh12", StringComparison.OrdinalIgnoreCase))
                {
                    return displayName;
                }

                WeaponAttachmentSocket actionSocket = GetComponentInChildren<WeaponAttachmentSocket>(true);
                return actionSocket != null
                    ? $"{displayName} [{actionSocket.ActionModeName}]"
                    : displayName;
            }

            WeaponAttachmentSocket attachmentSocket = GetComponentInChildren<WeaponAttachmentSocket>(true);
            return attachmentSocket != null
                ? $"{displayName} [{attachmentSocket.GripVariantName}]"
                : displayName;
        }
    }
    public string WeaponId => definition != null ? definition.id : weaponId;
    public string ModelId => definition != null ? definition.modelId : "ks23";

    public bool ToggleGripVariant()
    {
        return ToggleWeaponVariant();
    }

    public bool ToggleWeaponVariant()
    {
        if (!string.Equals(WeaponId, "ks23", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(WeaponId, "rsh12", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        WeaponAttachmentSocket attachmentSocket = GetComponentInChildren<WeaponAttachmentSocket>(true);
        if (attachmentSocket == null)
        {
            return false;
        }

        return string.Equals(WeaponId, "rsh12", StringComparison.OrdinalIgnoreCase)
            ? attachmentSocket.ToggleActionVariant()
            : attachmentSocket.ToggleGripVariant();
    }

    private bool IsDoubleAction => string.Equals(WeaponId, "rsh12", StringComparison.OrdinalIgnoreCase)
        && GetComponentInChildren<WeaponAttachmentSocket>(true)?.IsDoubleAction == true;

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
    public bool IsAimBlocked => IsReloading || reloadChamberUntil > Time.time;
    public float CrosshairChamberPressure
    {
        get
        {
            if (definition == null || ChamberProgress <= 0f)
            {
                return 0f;
            }

            float slowFireFactor = Mathf.InverseLerp(240f, 60f, definition.roundsPerMinute);
            if (definition.isTopLoad)
            {
                slowFireFactor = Mathf.Max(slowFireFactor, 0.75f);
            }

            return (1f - ChamberProgress) * slowFireFactor;
        }
    }
    public float ReloadProgress
    {
        get
        {
            if (!IsReloading || definition == null)
            {
                return 0f;
            }

            float duration = Mathf.Max(0.05f, definition.reloadDuration);
            return Mathf.Clamp01((Time.time - reloadStartedAt) / duration);
        }
    }
    public float ChamberProgress
    {
        get
        {
            if (definition == null)
            {
                return 0f;
            }

            if (reloadChamberUntil > Time.time)
            {
                float reloadChamberDuration = Mathf.Max(0.05f, reloadChamberUntil - reloadChamberStartedAt);
                return Mathf.Clamp01((Time.time - reloadChamberStartedAt) / reloadChamberDuration);
            }

            if (definition.automatic || chamberUntil <= Time.time)
            {
                return 0f;
            }

            float shotChamberDuration = Mathf.Max(0.05f, chamberUntil - chamberStartedAt);
            return Mathf.Clamp01((Time.time - chamberStartedAt) / shotChamberDuration);
        }
    }

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

        baseLocalRotation = transform.localRotation;
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
        if (PauseMenu.IsGameplayPaused || !equipped)
        {
            return;
        }

        UpdateAimRecoil();
        UpdateAimModel();

        if (ReloadPressedThisFrame())
        {
            BeginReload();
        }

        if (reloadUntil > 0f)
        {
            if (Time.time >= reloadUntil)
            {
                bool loaded = magazine.Reload(definition.isTopLoad);
                if (definition.isTopLoad && loaded && magazine.CurrentAmmo < magazine.Capacity && magazine.ReserveAmmo > 0)
                {
                    StartReloadInterval();
                }
                else
                {
                    bool needsChamber = loaded && !magazine.Chambered;
                    reloadUntil = 0f;
                    reloadStartedAt = 0f;
                    if (needsChamber)
                    {
                        StartChambering();
                    }
                }
            }

            return;
        }

        if (reloadChamberUntil > 0f)
        {
            if (Time.time >= reloadChamberUntil)
            {
                magazine.ChamberRound();
                reloadChamberUntil = 0f;
                reloadChamberStartedAt = 0f;
                if (IsRsh12)
                {
                    hammerCocked = true;
                }
            }

            return;
        }

        bool doubleAction = IsDoubleAction;
        bool triggerHeld = definition.automatic && ShootHeld();
        if (definition.automatic && !triggerHeld)
        {
            aimRecoilWavePhase = 0f;
        }

        bool shouldShoot = definition.automatic
            ? triggerHeld
            : doubleAction ? IsDoubleActionTriggerReady() : ShootPressedThisFrame();
        if (shouldShoot && TryShoot() && doubleAction)
        {
            doubleActionTriggerCommitted = true;
        }
    }

    private bool TryShoot()
    {
        if (playerController != null && !playerController.IsAiming)
        {
            return false;
        }

        if (IsRsh12 && !IsDoubleAction && !hammerCocked && Time.time >= chamberUntil)
        {
            hammerCocked = true;
        }

        if (IsRsh12 && !IsDoubleAction && !hammerCocked)
        {
            return false;
        }

        if (Time.time < nextShotTime || !magazine.ConsumeShot())
        {
            return false;
        }

        nextShotTime = Time.time + GetShotInterval();
        chamberStartedAt = Time.time;
        chamberUntil = chamberStartedAt + Mathf.Max(0.05f, definition.semiAutoCycleDuration);
        ApplyAimRecoil(ammoDefinition != null ? ammoDefinition.recoil : 0f);
        if (IsRsh12 && !IsDoubleAction)
        {
            hammerCocked = false;
        }
        ShotFired?.Invoke(ammoDefinition != null ? ammoDefinition.recoil : 0f);
        Transform shotOrigin = muzzle != null ? muzzle : transform;
        Vector3 aimPoint = GetCameraAimPoint(shotOrigin.position);
        Vector3 aimDirection = (aimPoint - shotOrigin.position).normalized;

        int pelletCount = Mathf.Max(1, ammoDefinition != null ? ammoDefinition.pellets : 1);
        float spreadAngle = ammoDefinition != null ? Mathf.Max(0f, ammoDefinition.moa) / 60f : 0f;
        for (int pelletIndex = 0; pelletIndex < pelletCount; pelletIndex++)
        {
            Vector3 pelletDirection = GetSpreadDirection(aimDirection, spreadAngle);
            Ray pelletRay = new Ray(shotOrigin.position, pelletDirection);
            if (TryGetFirstValidHit(pelletRay, out RaycastHit hit))
            {
                Damageable damageable = Damageable.FindInParent(hit.collider);
                if (damageable == null)
                {
                    damageable = ShootingTarget.FindOrAddForHit(hit.collider);
                }

                damageable?.ApplyHit(hit.collider, ammoDefinition);
                DrawTracer(pelletRay.origin, hit.point);
            }
            else
            {
                DrawTracer(pelletRay.origin, pelletRay.origin + pelletRay.direction * shotDistance);
            }
        }

        return true;
    }

    private bool IsDoubleActionTriggerReady()
    {
        if (!ShootHeld())
        {
            doubleActionTriggerHeld = false;
            doubleActionTriggerCommitted = false;
            return false;
        }

        if (!doubleActionTriggerHeld)
        {
            doubleActionTriggerHeld = true;
            doubleActionTriggerCommitted = false;
            doubleActionTriggerStartedAt = Time.time;
        }

        return !doubleActionTriggerCommitted
            && Time.time - doubleActionTriggerStartedAt >= Mathf.Max(0f, definition.doubleActionHoldDuration);
    }

    private bool IsRsh12 => string.Equals(WeaponId, "rsh12", StringComparison.OrdinalIgnoreCase);

    private static Vector3 GetSpreadDirection(Vector3 aimDirection, float spreadAngle)
    {
        if (spreadAngle <= 0f)
        {
            return aimDirection.normalized;
        }

        Quaternion aimRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
        Vector2 spread = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        return (aimRotation * new Vector3(spread.x, spread.y, 1f)).normalized;
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
        Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Quaternion baseWorldRotation = parentRotation * baseLocalRotation;
        if (playerController == null || !playerController.IsAiming)
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, baseLocalRotation, 1f - Mathf.Exp(-20f * Time.deltaTime));
            return;
        }

        Vector3 aimPoint = GetCameraAimPoint(transform.position);
        Vector3 aimDirection = aimPoint - transform.position;
        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            aimDirection = GetRecoilCameraRay(transform.position).direction;
        }

        Vector3 baseForwardAxis = baseWorldRotation * Vector3.forward;
        Quaternion targetWorldRotation = Quaternion.FromToRotation(baseForwardAxis, aimDirection.normalized) * baseWorldRotation;
        Quaternion targetLocalRotation = Quaternion.Inverse(parentRotation) * targetWorldRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, 1f - Mathf.Exp(-20f * Time.deltaTime));
    }

    private Vector3 GetCameraAimPoint(Vector3 fallbackOrigin)
    {
        Ray cameraRay = GetRecoilCameraRay(fallbackOrigin);
        if (TryGetFirstValidHit(cameraRay, out RaycastHit hit))
        {
            return hit.point;
        }

        return cameraRay.origin + cameraRay.direction * shotDistance;
    }

    private void UpdateAimRecoil()
    {
        aimRecoilPitchTarget = Mathf.MoveTowards(aimRecoilPitchTarget, 0f, aimRecoilRecoverySpeed * Time.deltaTime);
        aimRecoilYawTarget = Mathf.MoveTowards(aimRecoilYawTarget, 0f, aimRecoilRecoverySpeed * Time.deltaTime);
        aimRecoilPitchOffset = Mathf.SmoothDamp(
            aimRecoilPitchOffset,
            aimRecoilPitchTarget,
            ref aimRecoilPitchVelocity,
            aimRecoilSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);
        aimRecoilYawOffset = Mathf.SmoothDamp(
            aimRecoilYawOffset,
            aimRecoilYawTarget,
            ref aimRecoilYawVelocity,
            aimRecoilSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);
    }

    private void ApplyAimRecoil(float recoil)
    {
        float shotRecoil = Mathf.Max(0f, recoil);
        if (Time.time - lastAimRecoilShotTime > aimRecoilPatternResetDelay)
        {
            aimRecoilWavePhase = UnityEngine.Random.Range(-Mathf.PI, Mathf.PI);
        }

        float randomness = Mathf.Clamp01(definition != null ? definition.aimRecoilRandomness : 0.35f);
        float waveAmplitude = Mathf.Lerp(1f - randomness, 1f + randomness, UnityEngine.Random.value);
        float horizontalWave;
        if (definition != null && definition.automatic)
        {
            float phaseStep = Mathf.PI * UnityEngine.Random.Range(0.75f, 1.25f);
            aimRecoilWavePhase += phaseStep;
            horizontalWave = Mathf.Sin(aimRecoilWavePhase) * waveAmplitude;
        }
        else
        {
            horizontalWave = UnityEngine.Random.Range(-1f, 1f) * waveAmplitude;
        }
        aimRecoilPitchTarget = Mathf.Clamp(
            aimRecoilPitchTarget - shotRecoil * aimRecoilPitchMultiplier,
            -aimRecoilMaximum,
            0f);
        float horizontalMultiplier = definition != null
            ? Mathf.Max(0f, definition.aimRecoilYawMultiplier)
            : aimRecoilYawMultiplier;
        aimRecoilYawTarget = Mathf.Clamp(
            aimRecoilYawTarget
                + horizontalWave
                * shotRecoil
                * horizontalMultiplier,
            -aimRecoilMaximum,
            aimRecoilMaximum);
        lastAimRecoilShotTime = Time.time;
    }

    private bool TryGetFirstValidHit(Ray ray, out RaycastHit closestHit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            raycastBuffer,
            shotDistance,
            shotMask,
            QueryTriggerInteraction.Ignore);
        int closestIndex = -1;
        float closestDistance = float.PositiveInfinity;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = raycastBuffer[index];
            if (hit.collider == null || IsSelfHit(hit.collider) || hit.distance >= closestDistance)
            {
                continue;
            }

            closestIndex = index;
            closestDistance = hit.distance;
        }

        if (closestIndex < 0)
        {
            closestHit = default;
            return false;
        }

        closestHit = raycastBuffer[closestIndex];
        return true;
    }

    private bool IsSelfHit(Collider hitCollider)
    {
        if (hitCollider.transform.IsChildOf(transform.root))
        {
            return true;
        }

        return playerController != null
            && hitCollider.GetComponentInParent<PlayerController>() == playerController;
    }

    private static Ray GetCameraCenterRay(Vector3 origin)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return new Ray(origin, Vector3.forward);
        }

        return new Ray(camera.transform.position, camera.transform.forward);
    }

    private Ray GetRecoilCameraRay(Vector3 origin)
    {
        Ray cameraRay = GetCameraCenterRay(origin);
        Camera camera = Camera.main;
        Vector3 up = camera != null ? camera.transform.up : Vector3.up;
        Quaternion cameraRotation = Quaternion.LookRotation(cameraRay.direction, up);
        Vector3 recoilDirection = cameraRotation
            * Quaternion.Euler(aimRecoilPitchOffset, aimRecoilYawOffset, 0f)
            * Vector3.forward;
        return new Ray(cameraRay.origin, recoilDirection.normalized);
    }

    private void BeginReload()
    {
        if (IsReloading || magazine.CurrentAmmo >= magazine.Capacity || magazine.ReserveAmmo <= 0)
        {
            return;
        }

        StartReloadInterval();
    }

    private void StartReloadInterval()
    {
        reloadStartedAt = Time.time;
        reloadUntil = Time.time + Mathf.Max(0.05f, definition.reloadDuration);
    }

    private void StartChambering()
    {
        reloadChamberStartedAt = Time.time;
        reloadChamberUntil = Time.time + Mathf.Max(0.05f, definition.reloadChamberDuration);
    }

    private bool ReloadPressedThisFrame()
    {
        if (reloadAction != null)
        {
            return reloadAction.WasPressedThisFrame();
        }

        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    }

    private float GetShotInterval()
    {
        if (!definition.automatic)
        {
            return Mathf.Max(0.05f, definition.semiAutoCycleDuration);
        }

        return 60f / Mathf.Max(1f, definition.roundsPerMinute);
    }

    private bool ShootHeld()
    {
        bool actionHeld = shootAction != null && shootAction.IsPressed();
        bool mouseHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool gamepadHeld = Gamepad.current != null && Gamepad.current.rightTrigger.isPressed;
        return actionHeld || mouseHeld || gamepadHeld;
    }

    private bool ShootPressedThisFrame()
    {
        bool actionPressed = shootAction != null && shootAction.WasPressedThisFrame();
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool gamepadPressed = Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame;
        return actionPressed || mousePressed || gamepadPressed;
    }

    private WeaponDefinition LoadDefinition()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Weapons", weaponId + ".json");
        if (File.Exists(path))
        {
            try
            {
                WeaponDefinition loaded = JsonUtility.FromJson<WeaponDefinition>(File.ReadAllText(path));
                if (loaded != null)
                {
                    return loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not load weapon definition '{path}': {exception.Message}", this);
            }
        }

        if (fallbackDefinition != null)
        {
            try
            {
                WeaponDefinition loaded = JsonUtility.FromJson<WeaponDefinition>(fallbackDefinition.text);
                if (loaded != null)
                {
                    return loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not parse fallback weapon definition for '{weaponId}': {exception.Message}", this);
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

        try
        {
            return JsonUtility.FromJson<AmmoDefinition>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not load ammo definition '{path}': {exception.Message}");
            return null;
        }
    }
}
