using UnityEngine;

namespace CharacterSystem.Camera
{
    public class FirstPersonCamera : MonoBehaviour
    {
        [SerializeField] private CameraSettings settings;
        [SerializeField] private Transform playerBody;

        private CameraInputProvider inputProvider;
        private float pitch;
        private float yaw;

        private void Awake()
        {
            inputProvider = GetComponent<CameraInputProvider>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            pitch = transform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;

            if (playerBody != null)
            {
                yaw = playerBody.eulerAngles.y;
            }
        }

        private void Update()
        {
            if (settings == null) return;

            // Read input directly from the InputAction to avoid script
            // execution order issues with CameraInputProvider's cached value.
            Vector2 look = inputProvider != null && inputProvider.LookAction != null
                ? inputProvider.LookAction.ReadValue<Vector2>()
                : Vector2.zero;

            float mouseX = look.x * settings.HorizontalSensitivity;
            float mouseY = look.y * settings.VerticalSensitivity;

            // Yaw: apply to player body via absolute rotation to avoid
            // accumulation drift from Rotate() and ensure deterministic state.
            yaw += mouseX;
            if (playerBody != null)
                playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Pitch: apply to camera local rotation (doesn't affect body).
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, settings.MinPitch, settings.MaxPitch);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void SetPlayerBody(Transform body)
        {
            playerBody = body;
            if (playerBody != null)
                yaw = playerBody.eulerAngles.y;
        }
    }
}
