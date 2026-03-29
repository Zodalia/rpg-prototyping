using System.Collections.Generic;

public sealed class CombatLogger
{
    private readonly List<string> _log;
    private readonly BattleEventBus _bus;

    public CombatLogger(BattleEventBus bus, List<string> log)
    {
        _bus = bus;
        _log = log;
        _bus.EventRaised += OnEvent;
    }

    public void Unsubscribe()
    {
        _bus.EventRaised -= OnEvent;
    }

    private void OnEvent(BattleEvent e)
    {
        switch (e)
        {
            case ActionUsedEvent action:
                _log.Add($"{action.Actor.Definition.DisplayName} uses {action.Action.DisplayName}");
                break;

            case DamageDealtEvent damage:
                _log.Add($"{damage.Actor.Definition.DisplayName} hits {damage.Target.Definition.DisplayName} for {damage.Amount}");
                break;

            case UnitDefeatedEvent defeated:
                _log.Add($"{defeated.Unit.Definition.DisplayName} is defeated");
                break;

            case StatusAppliedEvent statusApplied:
                _log.Add($"{statusApplied.Target.Definition.DisplayName} gains {statusApplied.Status.DisplayName}");
                break;

            case StatusTickEvent statusTick:
                _log.Add($"{statusTick.Unit.Definition.DisplayName} takes {statusTick.Damage} from {statusTick.Status.DisplayName}");
                break;

            case StatusExpiredEvent statusExpired:
                _log.Add($"{statusExpired.Unit.Definition.DisplayName} loses {statusExpired.Status.DisplayName}");
                break;
        }
    }
}
