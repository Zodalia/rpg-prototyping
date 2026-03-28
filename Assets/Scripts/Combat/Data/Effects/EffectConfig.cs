using System;

[Serializable]
public abstract class EffectConfig
{
    public abstract string DisplayName { get; }
    public abstract void Apply(BattleState state, ActionExecution execution, CombatRules rules);
}