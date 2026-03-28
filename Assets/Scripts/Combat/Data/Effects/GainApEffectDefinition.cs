using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Effects/Gain AP")]
public sealed class GainApEffectDefinition : EffectDefinition
{
    [field: SerializeField] public int Amount { get; private set; } = 1;

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        execution.Actor.ActionPoints += Amount;
        state.Log.Add($"{execution.Actor.Definition.DisplayName} gains {Amount} AP");
    }
}