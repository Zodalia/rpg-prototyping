using UnityEngine;

public sealed class ResourceInstance
{
    public ResourceDefinition Definition { get; }
    public int MaxValue { get; }

    private int _currentValue;
    public int CurrentValue
    {
        get => _currentValue;
        set => _currentValue = MaxValue >= 0 ? Mathf.Clamp(value, 0, MaxValue) : value;
    }

    public ResourceInstance(ResourceDefinition definition, int initialValue, int maxValue = -1)
    {
        Definition = definition;
        MaxValue = maxValue;
        _currentValue = maxValue >= 0 ? Mathf.Clamp(initialValue, 0, maxValue) : initialValue;
    }

    public int AvailableCapacity => MaxValue >= 0 ? MaxValue - _currentValue : int.MaxValue;

    public void Decay()
    {
        if (Definition != null)
            CurrentValue = Mathf.Max(0, CurrentValue - Definition.DecayPerTurn);
    }
}