#nullable enable
using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages the shared team currency. Server-authoritative: only the server
/// can award or deduct currency. All clients can read the current value
/// via the NetworkVariable.
/// </summary>
public class CurrencyManager : NetworkBehaviour
{
    public static CurrencyManager? Instance { get; private set; }

    [Header("Fragment Value")]
    [SerializeField] private float baseValuePerCubicMeter = 100f;
    [SerializeField] private float minDepositValue = 5f;

    private readonly NetworkVariable<int> _teamCurrency = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Current team currency. Readable by all clients.</summary>
    public int TeamCurrency => _teamCurrency.Value;

    /// <summary>Fires on all clients when currency changes. Args: (oldValue, newValue).</summary>
    public event Action<int, int>? OnCurrencyChanged;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        _teamCurrency.OnValueChanged += HandleCurrencyChanged;
    }

    public override void OnNetworkDespawn()
    {
        _teamCurrency.OnValueChanged -= HandleCurrencyChanged;
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Calculates currency value from a fragment's renderer bounds volume.
    /// Call on server only, before the fragment is destroyed.
    /// </summary>
    public int CalculateFragmentValue(Renderer? renderer)
    {
        if (renderer == null)
            return Mathf.CeilToInt(minDepositValue);

        var size = renderer.bounds.size;
        float volume = size.x * size.y * size.z;
        float value = volume * baseValuePerCubicMeter;

        return Mathf.Max(Mathf.CeilToInt(value), Mathf.CeilToInt(minDepositValue));
    }

    /// <summary>
    /// Awards currency to the team. Server only.
    /// </summary>
    public void AwardCurrency(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;

        _teamCurrency.Value += amount;
    }

    /// <summary>
    /// Attempts to deduct currency. Returns true if the team had enough.
    /// Server only.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (!IsServer) return false;
        if (amount <= 0) return false;
        if (_teamCurrency.Value < amount) return false;

        _teamCurrency.Value -= amount;
        return true;
    }

    private void HandleCurrencyChanged(int oldValue, int newValue)
    {
        OnCurrencyChanged?.Invoke(oldValue, newValue);
    }
}
