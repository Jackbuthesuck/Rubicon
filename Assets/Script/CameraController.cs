using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);
    [SerializeField] private Vector3 cameraOffset = new Vector3(0.65f, 1.6f, -4.2f);
    [SerializeField] private Vector3 aimTargetOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private Vector3 aimCameraOffset = new Vector3(1.25f, 2.5f, -1.5f);
    [SerializeField] private Vector3 leftAimCameraOffset = new Vector3(-1.25f, 2.5f, -1.5f);
    [SerializeField] private Vector3 frontAimCameraOffset = new Vector3(0f, 2.5f, -1.5f);
    [SerializeField, Min(0f)] private float aimLookAhead = 1.6f;
    [SerializeField, Min(0.01f)] private float aimTransitionSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float aimOrbitPointBlend = 0.35f;
    [SerializeField] private float aimOrbitForwardOffset = 0.5f;
    [SerializeField] private float aimUpCameraHeight = 0.23f;
    [SerializeField] private float aimDownCameraHeight = 2.5f;
    [SerializeField, Min(0.01f)] private float aimCameraHeightBias = 2.5f;

    [Header("Look")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float startingPitch = 12f;
    [SerializeField] private float minimumPitch = -25f;
    [SerializeField] private float maximumPitch = 55f;
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float gamepadSensitivity = 180f;

    [Header("Follow")]
    [SerializeField, Min(0f)] private float positionSharpness = 18f;
    [SerializeField, Min(0f)] private float rotationSharpness = 18f;

    [Header("Collision")]
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
    [SerializeField, Min(0.1f)] private float collisionPadding = 0.1f;
    [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;

    private Camera cameraComponent;
    private PlayerController player;
    private InputAction lookAction;
    private InputAction primaryWheelAction;
    private InputAction secondaryWheelAction;
    private float yaw;
    private float pitch;
    private float aimBlend;
    private bool cameraParented;
    private AimCameraPosition aimCameraPosition = AimCameraPosition.RightShoulder;
    private int aimCameraTransitionVersion;
    private float aimCameraTransitionDirection;

    public Vector3 MovementForward
    {
        get
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }
    }

    public Vector3 MovementRight
    {
        get
        {
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
        }
    }

    public float LookHorizontalForCrosshair
    {
        get
        {
            if (lookAction == null)
            {
                return 0f;
            }

            Vector2 lookInput = lookAction.ReadValue<Vector2>();
            if (lookAction.activeControl?.device is Gamepad)
            {
                return Mathf.Clamp(lookInput.x, -1f, 1f);
            }

            if (lookAction.activeControl?.device is Mouse)
            {
                return Mathf.Clamp(lookInput.x * mouseSensitivity / 4f, -1f, 1f);
            }

            return 0f;
        }
    }

    public int AimCameraTransitionVersion => aimCameraTransitionVersion;
    public float AimCameraTransitionDirection => aimCameraTransitionDirection;

    private void MarkAimCameraTransition(float direction)
    {
        aimCameraTransitionDirection = direction;
        aimCameraTransitionVersion++;
    }

    private enum AimCameraPosition
    {
        RightShoulder,
        LeftShoulder,
        Front
    }

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);

        if (target == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (player == null && target != null)
        {
            player = target.GetComponent<PlayerController>();
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
            transform.SetParent(null, true);
            cameraParented = false;
        }
        else
        {
            Debug.LogWarning($"{nameof(CameraController)} needs a target PlayerController in the scene.", this);
        }

        if (inputActions == null)
        {
            Debug.LogWarning($"{nameof(CameraController)} needs an InputActionAsset assigned.", this);
        }
        else
        {
            lookAction = inputActions.FindAction("Player/Look", true);
            primaryWheelAction = inputActions.FindAction("Player/Primary Wheel", false);
            secondaryWheelAction = inputActions.FindAction("Player/Secondary Wheel", false);
        }

        SnapToTarget();
    }

    private void OnEnable()
    {
        lookAction?.Enable();
        primaryWheelAction?.Enable();
        secondaryWheelAction?.Enable();
    }

    private void OnDisable()
    {
        lookAction?.Disable();
        primaryWheelAction?.Disable();
        secondaryWheelAction?.Disable();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        bool isAiming = player != null && player.IsAiming;
        UpdateOrbit();
        if (isAiming)
        {
            SetTargetYaw();
        }

        UpdateAimCameraPosition(isAiming);
        UpdateCameraParenting(isAiming);
        aimBlend = Mathf.MoveTowards(aimBlend, isAiming ? 1f : 0f, aimTransitionSpeed * Time.deltaTime);

        Vector3 currentTargetOffset = Vector3.Lerp(targetOffset, aimTargetOffset, aimBlend);
        Vector3 selectedAimCameraOffset = GetAimCameraOffset();
        Vector3 currentCameraOffset = Vector3.Lerp(cameraOffset, selectedAimCameraOffset, aimBlend);
        if (cameraParented)
        {
            UpdateParentedCamera(currentTargetOffset, currentCameraOffset);
        }
        else
        {
            UpdateOrbitCamera(currentTargetOffset, currentCameraOffset);
        }

        if (cameraComponent != null)
        {
            cameraComponent.fieldOfView = Mathf.Lerp(60f, 48f, aimBlend);
        }
    }

    private void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 pivot = target.TransformPoint(targetOffset);
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = pivot + orbitRotation * (cameraOffset - targetOffset);
        desiredPosition = ResolveCollision(pivot, desiredPosition);
        transform.SetPositionAndRotation(desiredPosition, Quaternion.LookRotation(pivot - desiredPosition, Vector3.up));
        cameraParented = false;
        Debug.Log($"{nameof(CameraController)} following {target.name} at {target.position}.", this);
    }

    private void UpdateCameraParenting(bool shouldParent)
    {
        if (cameraParented == shouldParent)
        {
            return;
        }

        if (shouldParent)
        {
            transform.SetParent(target, true);
        }
        else
        {
            yaw = target.eulerAngles.y;
            transform.SetParent(null, true);
        }

        cameraParented = shouldParent;
    }

    private void UpdateParentedCamera(Vector3 pivotOffset, Vector3 cameraOffset)
    {
        Vector3 orbitOffset = Vector3.Lerp(pivotOffset, cameraOffset, aimOrbitPointBlend);
        orbitOffset += Vector3.forward * aimOrbitForwardOffset;
        Vector3 desiredLocalPosition = cameraOffset;
        float pitchAmount = Mathf.InverseLerp(minimumPitch, maximumPitch, pitch);
        pitchAmount = Mathf.Pow(pitchAmount, aimCameraHeightBias);
        desiredLocalPosition.y = Mathf.Lerp(aimUpCameraHeight, aimDownCameraHeight, pitchAmount);
        Vector3 pivot = target.TransformPoint(orbitOffset);
        Vector3 desiredPosition = ResolveCollision(pivot, target.TransformPoint(desiredLocalPosition));
        desiredLocalPosition = target.InverseTransformPoint(desiredPosition);

        float positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(transform.localPosition, desiredLocalPosition, positionBlend);

        Quaternion desiredLocalRotation = Quaternion.Euler(pitch, 0f, 0f);
        float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, desiredLocalRotation, rotationBlend);
    }

    private void UpdateOrbitCamera(Vector3 pivotOffset, Vector3 cameraOffset)
    {
        Vector3 pivot = target.TransformPoint(pivotOffset);
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = pivot + orbitRotation * (cameraOffset - pivotOffset);
        desiredPosition = ResolveCollision(pivot, desiredPosition);

        float positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

        Vector3 lookTarget = pivot + orbitRotation * (Vector3.forward * (aimLookAhead * aimBlend));
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }
    }

    private Vector3 GetOrbitCameraPosition(Vector3 pivotOffset, Vector3 cameraOffset)
    {
        return pivotOffset + Quaternion.Euler(pitch, 0f, 0f) * (cameraOffset - pivotOffset);
    }

    private bool UpdateOrbit()
    {
        if ((primaryWheelAction != null && primaryWheelAction.IsPressed())
            || (secondaryWheelAction != null && secondaryWheelAction.IsPressed()))
        {
            return false;
        }

        Vector2 lookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        bool isMouse = lookAction?.activeControl?.device is Mouse;
        float multiplier = isMouse ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

        yaw += lookInput.x * multiplier;
        pitch = Mathf.Clamp(pitch - lookInput.y * multiplier, minimumPitch, maximumPitch);
        return lookInput.sqrMagnitude > 0.0001f;
    }

    private void SetTargetYaw()
    {
        target.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void UpdateAimCameraPosition(bool isAiming)
    {
        if (!isAiming)
        {
            return;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
        {
            return;
        }

        if (gamepad.dpad.right.wasPressedThisFrame)
        {
            aimCameraPosition = AimCameraPosition.RightShoulder;
            MarkAimCameraTransition(1f);
        }
        else if (gamepad.dpad.left.wasPressedThisFrame)
        {
            aimCameraPosition = AimCameraPosition.LeftShoulder;
            MarkAimCameraTransition(-1f);
        }
        else if (gamepad.dpad.up.wasPressedThisFrame)
        {
            aimCameraPosition = AimCameraPosition.Front;
            MarkAimCameraTransition(0f);
        }
    }

    private Vector3 GetAimCameraOffset()
    {
        switch (aimCameraPosition)
        {
            case AimCameraPosition.LeftShoulder:
                return leftAimCameraOffset;
            case AimCameraPosition.Front:
                return frontAimCameraOffset;
            default:
                return aimCameraOffset;
        }
    }

    private Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 toCamera = desiredPosition - pivot;
        float distance = toCamera.magnitude;

        if (distance <= collisionRadius)
        {
            return desiredPosition;
        }

        Vector3 direction = toCamera / distance;
        RaycastHit[] hits = Physics.SphereCastAll(pivot, collisionRadius, direction, distance, collisionMask, QueryTriggerInteraction.Ignore);
        float nearestDistance = distance;
        bool hasCollision = false;
        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            Collider hitCollider = hits[hitIndex].collider;
            if (hitCollider == null || IsPlayerCollider(hitCollider))
            {
                continue;
            }

            if (hits[hitIndex].distance < nearestDistance)
            {
                nearestDistance = hits[hitIndex].distance;
                hasCollision = true;
            }
        }

        if (hasCollision)
        {
            return pivot + direction * Mathf.Max(0f, nearestDistance - collisionPadding);
        }

        return desiredPosition;
    }

    private bool IsPlayerCollider(Collider hitCollider)
    {
        if (hitCollider.transform == target || (target != null && hitCollider.transform.IsChildOf(target)))
        {
            return true;
        }

        return player != null && hitCollider.GetComponentInParent<PlayerController>() == player;
    }
}