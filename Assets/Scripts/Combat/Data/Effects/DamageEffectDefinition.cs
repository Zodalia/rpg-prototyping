using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Effects/Damage")]
public sealed class DamageEffectDefinition : EffectDefinition
{
    [field: SerializeField] public int Power { get; private set; } = 0;

    public override void Apply(BattleState state, ActionExecution execution, CombatRules rules)
    {
        foreach (var target in execution.Targets)
        {
            int damage = rules.CalculateDamage(execution.Actor, target, Power);
            target.Hp -= damage;

            state.Log.Add($"{execution.Actor.Definition.DisplayName} hits {target.Definition.DisplayName} for {damage}");

            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.IsAlive = false;
                state.Log.Add($"{target.Definition.DisplayName} is defeated");
            }
        }
    }
}