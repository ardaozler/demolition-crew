using UnityEngine;

namespace CharacterSystem.Camera
{
    public class FirstPersonCamera : MonoBehaviour
    {
        [SerializeField] private CameraSettings settings;
        [SerializeField] private Transform playerBody;

        private CameraInputProvider inputProvider;
        private float pitch;

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
        }

        private void LateUpdate()
        {
            if (settings == null) return;

            HandleRotation();
        }

        private void HandleRotation()
        {
            Vector2 look = inputProvider != null ? inputProvider.LookInput : Vector2.zero;

            float mouseX = look.x * settings.HorizontalSensitivity;
            float mouseY = look.y * settings.VerticalSensitivity;

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, settings.MinPitch, settings.MaxPitch);

            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (playerBody != null)
                playerBody.Rotate(Vector3.up * mouseX);
        }

        public void SetPlayerBody(Transform body)
        {
            playerBody = body;
        }
    }
}
