using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterSystem.Handlers
{
    public class InputProvider : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string useActionName = "Use";
        [SerializeField] private string interactActionName = "Interact";

        public Vector2 MoveInput { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }

        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnUseStarted;
        public event Action OnUseCanceled;
        public event Action OnInteractStarted;

        public InputActionAsset InputActions => inputActions;
        public InputAction MoveAction { get; private set; }
        public InputAction JumpAction { get; private set; }
        public InputAction UseAction { get; private set; }
        public InputAction InteractAction { get; private set; }

        private InputActionMap actionMap;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("InputProvider: InputActionAsset is not assigned!", this);
                enabled = false;
                return;
            }

            actionMap = inputActions.FindActionMap(actionMapName);
            MoveAction = actionMap.FindAction(moveActionName);
            JumpAction = actionMap.FindAction(jumpActionName);
            UseAction = actionMap.FindAction(useActionName);
            InteractAction = actionMap.FindAction(interactActionName);
            actionMap.Enable();
        }

        private void OnEnable()
        {
            if (JumpAction != null)
            {
                JumpAction.started += OnJumpStarted;
                JumpAction.canceled += OnJumpCanceled;
            }

            if (UseAction != null)
            {
                UseAction.started += HandleUseStarted;
                UseAction.canceled += HandleUseCanceled;
            }

            if (InteractAction != null)
            {
                InteractAction.started += HandleInteractStarted;
            }
        }

        private void OnDisable()
        {
            if (JumpAction != null)
            {
                JumpAction.started -= OnJumpStarted;
                JumpAction.canceled -= OnJumpCanceled;
            }

            if (UseAction != null)
            {
                UseAction.started -= HandleUseStarted;
                UseAction.canceled -= HandleUseCanceled;
            }

            if (InteractAction != null)
            {
                InteractAction.started -= HandleInteractStarted;
            }
        }

        private void Update()
        {
            if (MoveAction == null) return;

            MoveInput = MoveAction.ReadValue<Vector2>();

            if (MoveInput.sqrMagnitude > 1f)
                MoveInput = MoveInput.normalized;

            JumpHeld = JumpAction.IsPressed();
            JumpPressed = JumpAction.WasPressedThisFrame();
        }

        private void OnJumpStarted(InputAction.CallbackContext context) => OnJumpPressed?.Invoke();
        private void OnJumpCanceled(InputAction.CallbackContext context) => OnJumpReleased?.Invoke();
        private void HandleUseStarted(InputAction.CallbackContext context) => OnUseStarted?.Invoke();
        private void HandleUseCanceled(InputAction.CallbackContext context) => OnUseCanceled?.Invoke();
        private void HandleInteractStarted(InputAction.CallbackContext context) => OnInteractStarted?.Invoke();
    }
}