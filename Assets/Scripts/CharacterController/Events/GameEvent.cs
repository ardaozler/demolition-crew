using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterSystem.Events
{
    [CreateAssetMenu(menuName = "Character/Events/Game Event")]
    public class GameEvent : ScriptableObject
    {
        private readonly List<Action> listeners = new List<Action>();

        public void Raise()
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                listeners[i]?.Invoke();
            }
        }

        public void Register(Action listener)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }

        public void Unregister(Action listener)
        {
            listeners.Remove(listener);
        }
    }
}