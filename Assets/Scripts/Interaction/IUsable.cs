using UnityEngine;

namespace InteractionSystem
{
    public interface IUsable
    {
        string UsableName { get; }
        void OnEquip(GameObject owner);
        void OnUnequip(GameObject owner);
        void OnUseStarted(GameObject owner);
        void OnUseStopped(GameObject owner);
    }
}
