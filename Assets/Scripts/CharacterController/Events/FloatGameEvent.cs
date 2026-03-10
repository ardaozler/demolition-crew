using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterSystem.Events
{
    [CreateAssetMenu(menuName = "Character/Events/Float Game Event")]
    public class FloatGameEvent : ScriptableObject
    {
        private readonly List<Action<float>> listeners = new List<Action<float>>();

        public void Raise(float value)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i]?.Invoke(value);
            }
        }

        public void Register(Action<float> listener)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }

        public void Unregister(Action<float> listener)
        {
            listeners.Remove(listener);
        }
    }
}