using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public sealed class BattleScreen : MonoBehaviour
{
    [SerializeField] private BattleController battleController;
    [SerializeField] private Transform actionButtonRoot;
    [SerializeField] private ActionButtonView actionButtonPrefab;
    [SerializeField] private TextMeshProUGUI logText;

    private UnitState _pendingActor;

    private void OnEnable()
    {
        battleController.StateChanged += Refresh;
        battleController.PlayerInputRequested += ShowPlayerActions;
        battleController.BattleEnded += OnBattleEnded;
    }

    private void OnDisable()
    {
        battleController.StateChanged -= Refresh;
        battleController.PlayerInputRequested -= ShowPlayerActions;
        battleController.BattleEnded -= OnBattleEnded;
    }

    private void Refresh()
    {
        if (battleController.State == null)
            return;

        logText.text = string.Join("\n", battleController.State.Log.TakeLast(12));
    }

    private void ShowPlayerActions(UnitState actor, List<ActionDefinition> actions)
    {
        _pendingActor = actor;

        foreach (Transform child in actionButtonRoot)
            Destroy(child.gameObject);

        foreach (var action in actions)
        {
            var button = Instantiate(actionButtonPrefab, actionButtonRoot);
            button.Bind(action, () => OnActionChosen(action));
        }
    }

    private void OnActionChosen(ActionDefinition action)
    {
        var state = battleController.State;
        List<UnitState> targets;

        switch (action.Targeting)
        {
            case ActionDefinition.TargetType.Self:
                targets = new List<UnitState> { _pendingActor };
                break;

            case ActionDefinition.TargetType.SingleEnemy:
                targets = new List<UnitState> { state.GetEnemiesOf(_pendingActor).First() };
                break;

            case ActionDefinition.TargetType.AllEnemies:
                targets = state.GetEnemiesOf(_pendingActor).ToList();
                break;

            case ActionDefinition.TargetType.SingleAlly:
                targets = new List<UnitState> { state.GetAlliesOf(_pendingActor).First() };
                break;

            case ActionDefinition.TargetType.AllAllies:
                targets = state.GetAlliesOf(_pendingActor).ToList();
                break;

            default:
                targets = new List<UnitState>();
                break;
        }

        battleController.SubmitAction(new ActionExecution(_pendingActor, action, targets));
    }

    private void OnBattleEnded(string winner)
    {
        logText.text += $"\n\n{winner} wins.";
    }
}