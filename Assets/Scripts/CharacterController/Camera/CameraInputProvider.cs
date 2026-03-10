using UnityEngine;
using UnityEngine.InputSystem;

namespace CharacterSystem.Camera
{
    public class CameraInputProvider : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Camera";
        [SerializeField] private string lookActionName = "Look";

        public Vector2 LookInput { get; private set; }

        public InputActionAsset InputActions => inputActions;
        public InputAction LookAction { get; private set; }

        private InputActionMap actionMap;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("CameraInputProvider: InputActionAsset is not assigned!", this);
                enabled = false;
                return;
            }

            actionMap = inputActions.FindActionMap(actionMapName);
            if (actionMap == null)
            {
                actionMap = inputActions.FindActionMap("Player");
            }

            if (actionMap == null)
            {
                Debug.LogError("CameraInputProvider: Could not find action map!", this);
                enabled = false;
                return;
            }

            LookAction = actionMap.FindAction(lookActionName);
            actionMap.Enable();
        }

        private void Update()
        {
            if (LookAction == null) return;
            LookInput = LookAction.ReadValue<Vector2>();
        }
    }
}
