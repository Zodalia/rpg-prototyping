using UnityEngine;

public sealed class ResourceInstance
{
    public ResourceDefinition Definition { get; }
    public int CurrentValue { get; set; }

    public ResourceInstance(ResourceDefinition definition, int initialValue)
    {
        Definition = definition;
        CurrentValue = initialValue;
    }

    public void Decay()
    {
        if (Definition != null)
            CurrentValue = Mathf.Max(0, CurrentValue - Definition.DecayPerTurn);
    }
}