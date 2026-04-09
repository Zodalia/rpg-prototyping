using System;
using UnityEngine;

[Serializable]
public sealed class DamageEffectConfig : EffectConfig
{
    [SerializeField] private int power = 0;

    public override string DisplayName => "Damage";

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        foreach (var target in ResolveTargets(state, execution))
        {
            int damage = rules.CalculateDamage(execution.Actor, target, power + execution.PowerModifier);
            target.Hp -= damage;

            bool isLethal = rules.CheckAndApplyDeath(state, target, execution.Actor);
            state.EventBus.Raise(new DamageDealtEvent(state.TurnNumber, execution.Actor, target, damage, isLethal));
        }
    }
}