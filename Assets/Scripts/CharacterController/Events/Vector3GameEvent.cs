using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterSystem.Events
{
    [CreateAssetMenu(menuName = "Character/Events/Vector3 Game Event")]
    public class Vector3GameEvent : ScriptableObject
    {
        private readonly List<Action<Vector3>> listeners = new List<Action<Vector3>>();

        public void Raise(Vector3 value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i]?.Invoke(value);
            }
        }

        public void Register(Action<Vector3> listener)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }

        public void Unregister(Action<Vector3> listener)
        {
            listeners.Remove(listener);
        }
    }
}