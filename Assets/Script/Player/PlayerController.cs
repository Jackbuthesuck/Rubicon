using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 3.5f;
    [SerializeField, Min(0f)] private float aimSpeed = 2.2f;
    [SerializeField, Min(0f)] private float acceleration = 18f;
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;

    [Header("Grounding")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedVerticalSpeed = -2f;

    [Header("Camera")]
    [SerializeField] private Transform movementReference;

    private CharacterController characterController;
    private CameraController cameraController;
    private InputAction moveAction;
    private InputAction aimAction;
    private Vector3 planarVelocity;
    private float verticalVelocity;

    public bool IsAiming { get; private set; }
    public Vector3 PlanarVelocity => planarVelocity;
    public Vector3 LocalPlanarVelocity => transform.InverseTransformDirection(planarVelocity);
    public float MoveInputHorizontal { get; private set; }
    public float MovementSpeed => walkSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (movementReference == null && Camera.main != null)
        {
            movementReference = Camera.main.transform;
        }

        if (movementReference != null)
        {
            cameraController = movementReference.GetComponent<CameraController>();
        }

        if (inputActions == null)
        {
            Debug.LogWarning($"{nameof(PlayerController)} needs an InputActionAsset assigned.", this);
            return;
        }

        moveAction = inputActions.FindAction("Player/Move", true);
        aimAction = inputActions.FindAction("Player/Aim", true);
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        aimAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        aimAction?.Disable();
    }

    private void Update()
    {
        if (PauseMenu.IsGameplayPaused)
        {
            return;
        }

        Vector2 input = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        MoveInputHorizontal = input.x;
        bool isAiming = aimAction?.IsPressed() ?? false;
        IsAiming = isAiming;

        Vector3 moveDirection = GetCameraRelativeDirection(input);
        float targetSpeed = isAiming ? aimSpeed : walkSpeed;
        Vector3 targetVelocity = moveDirection * targetSpeed;
        planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, acceleration * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalSpeed;
        }

        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        RotateCharacter(moveDirection, isAiming);
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 referenceForward = cameraController != null ? cameraController.MovementForward : movementReference != null ? movementReference.forward : Vector3.forward;
        Vector3 referenceRight = cameraController != null ? cameraController.MovementRight : movementReference != null ? movementReference.right : Vector3.right;

        referenceForward.y = 0f;
        referenceRight.y = 0f;
        referenceForward.Normalize();
        referenceRight.Normalize();

        return Vector3.ClampMagnitude(referenceRight * input.x + referenceForward * input.y, 1f);
    }

    private void RotateCharacter(Vector3 moveDirection, bool isAiming)
    {
        Vector3 facingDirection = isAiming && movementReference != null
            ? Vector3.ProjectOnPlane(movementReference.forward, Vector3.up)
            : moveDirection;

        if (facingDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}