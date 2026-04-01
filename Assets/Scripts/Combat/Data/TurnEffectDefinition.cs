using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Turn Effect Definition")]
public sealed class TurnEffectDefinition : ScriptableObject
{
    [field: SerializeField] public TurnTickTiming Timing { get; private set; } = TurnTickTiming.TurnEnd;
    [field: SerializeField] public TurnEffectTargetScope TargetScope { get; private set; } = TurnEffectTargetScope.Self;
    [field: SerializeReference] public List<EffectConfig> Effects { get; private set; } = new();
}
