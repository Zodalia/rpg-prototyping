using UnityEngine;

public abstract class EffectDefinition : ScriptableObject
{
    public abstract void Apply(BattleState state, ActionExecution execution, CombatRules rules);
}