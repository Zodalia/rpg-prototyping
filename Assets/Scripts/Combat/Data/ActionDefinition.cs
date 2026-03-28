using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Action Definition")]
public sealed class ActionDefinition : ScriptableObject
{
    public enum TargetType
    {
        Self,
        SingleAlly,
        SingleEnemy,
        AllAllies,
        AllEnemies
    }

    [field: SerializeField] public string Id { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField, TextArea] public string Description { get; private set; }

    [field: SerializeField] public TargetType Targeting { get; private set; } = TargetType.SingleEnemy;

    [field: SerializeField] public int Cooldown { get; private set; } = 0;
    [field: SerializeField] public int SpeedModifier { get; private set; } = 0;

    [field: SerializeField] public List<ResourceRequirement> ResourceRequirements { get; private set; } = new();
    [field: SerializeReference] public List<EffectConfig> Effects { get; private set; } = new();
}