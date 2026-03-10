using UnityEngine;
using UnityEngine.Events;

namespace CharacterSystem.Events
{
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private GameEvent gameEvent;
        [SerializeField] private UnityEvent response;

        private void OnEnable()
        {
            if (gameEvent != null)
                gameEvent.Register(OnEventRaised);
        }

        private void OnDisable()
        {
            if (gameEvent != null)
                gameEvent.Unregister(OnEventRaised);
        }

        private void OnEventRaised()
        {
            response?.Invoke();
        }
    }
}