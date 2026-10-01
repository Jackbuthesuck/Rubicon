using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.45f, 0f);
    [SerializeField] private Vector3 cameraOffset = new Vector3(0.65f, 0.35f, -3.8f);
    [SerializeField] private Vector3 aimTargetOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 aimCameraOffset = new Vector3(0.55f, 0.25f, -2.2f);
    [SerializeField, Min(0.01f)] private float aimTransitionSpeed = 8f;

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
    private Vector3 currentVelocity;
    private float yaw;
    private float pitch;
    private float aimBlend;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        pitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);

        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
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

        UpdateOrbit();

        bool isAiming = player != null && player.IsAiming;
        aimBlend = Mathf.MoveTowards(aimBlend, isAiming ? 1f : 0f, aimTransitionSpeed * Time.deltaTime);

        Vector3 currentTargetOffset = Vector3.Lerp(targetOffset, aimTargetOffset, aimBlend);
        Vector3 currentCameraOffset = Vector3.Lerp(cameraOffset, aimCameraOffset, aimBlend);
        Vector3 pivot = target.position + currentTargetOffset;
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = pivot + orbitRotation * currentCameraOffset;
        desiredPosition = ResolveCollision(pivot, desiredPosition);

        float positionBlend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

        Vector3 lookDirection = pivot - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
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

        Vector3 pivot = target.position + targetOffset;
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPosition = ResolveCollision(pivot, pivot + orbitRotation * cameraOffset);
        transform.SetPositionAndRotation(desiredPosition, Quaternion.LookRotation(pivot - desiredPosition, Vector3.up));
        Debug.Log($"{nameof(CameraController)} following {target.name} at {target.position}.", this);
    }

    private void UpdateOrbit()
    {
        if ((primaryWheelAction != null && primaryWheelAction.IsPressed())
            || (secondaryWheelAction != null && secondaryWheelAction.IsPressed()))
        {
            return;
        }

        Vector2 lookInput = lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        bool isMouse = lookAction?.activeControl?.device is Mouse;
        float multiplier = isMouse ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

        yaw += lookInput.x * multiplier;
        pitch = Mathf.Clamp(pitch - lookInput.y * multiplier, minimumPitch, maximumPitch);
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
        if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            return pivot + direction * Mathf.Max(0f, hit.distance - collisionPadding);
        }

        return desiredPosition;
    }
}