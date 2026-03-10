using UnityEngine;

namespace CharacterSystem.Camera
{
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "Character/Settings/Camera")]
    public class CameraSettings : ScriptableObject
    {
        [Header("Sensitivity")]
        [Tooltip("Horizontal mouse sensitivity.")]
        [SerializeField] private float horizontalSensitivity = 2f;

        [Tooltip("Vertical mouse sensitivity.")]
        [SerializeField] private float verticalSensitivity = 2f;

        [Header("Pitch Clamp")]
        [Tooltip("Minimum pitch angle (looking up).")]
        [SerializeField] private float minPitch = -89f;

        [Tooltip("Maximum pitch angle (looking down).")]
        [SerializeField] private float maxPitch = 89f;

        public float HorizontalSensitivity => horizontalSensitivity;
        public float VerticalSensitivity => verticalSensitivity;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
    }
}
