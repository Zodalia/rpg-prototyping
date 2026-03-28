using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class BattleScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleController battleController;

    [Header("Unit Panels")]
    [SerializeField] private Transform playerUnitsRoot;
    [SerializeField] private Transform enemyUnitsRoot;
    [SerializeField] private UnitView unitViewPrefab;

    [Header("Actions")]
    [SerializeField] private Transform actionButtonRoot;
    [SerializeField] private ActionButtonView actionButtonPrefab;
    [SerializeField] private Button cancelTargetingButton;

    [Header("Log")]
    [SerializeField] private TextMeshProUGUI logText;

    private readonly List<UnitView> _unitViews = new();
    private UnitState _pendingActor;
    private ActionDefinition _pendingAction;

    private void OnEnable()
    {
        battleController.StateChanged += Refresh;
        battleController.PlayerInputRequested += ShowPlayerActions;
        battleController.BattleEnded += OnBattleEnded;

        if (cancelTargetingButton != null)
        {
            cancelTargetingButton.onClick.AddListener(CancelTargeting);
            cancelTargetingButton.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        battleController.StateChanged -= Refresh;
        battleController.PlayerInputRequested -= ShowPlayerActions;
        battleController.BattleEnded -= OnBattleEnded;

        if (cancelTargetingButton != null)
            cancelTargetingButton.onClick.RemoveListener(CancelTargeting);
    }

    private void Refresh()
    {
        if (battleController.State == null)
            return;

        SpawnUnitViewsIfNeeded();

        var activeUnit = battleController.State.ActiveUnit;
        foreach (var view in _unitViews)
            view.Refresh(activeUnit);

        logText.text = string.Join("\n", battleController.State.Log.TakeLast(12));
    }

    private void SpawnUnitViewsIfNeeded()
    {
        if (_unitViews.Count > 0)
            return;

        foreach (var unit in battleController.State.Units)
        {
            var root = unit.Team == "Player" ? playerUnitsRoot : enemyUnitsRoot;
            var view = Instantiate(unitViewPrefab, root);
            view.Bind(unit);
            _unitViews.Add(view);
        }
    }

    private void ShowPlayerActions(UnitState actor, List<ActionDefinition> actions)
    {
        _pendingActor = actor;
        _pendingAction = null;
        ShowActionButtons(actions);
    }

    private void ShowActionButtons(List<ActionDefinition> actions)
    {
        ClearActionButtons();
        ClearTargeting();

        if (cancelTargetingButton != null)
            cancelTargetingButton.gameObject.SetActive(false);

        foreach (var action in actions)
        {
            var button = Instantiate(actionButtonPrefab, actionButtonRoot);
            button.Bind(action, () => OnActionChosen(action));
        }
    }

    private void OnActionChosen(ActionDefinition action)
    {
        var state = battleController.State;

        switch (action.Targeting)
        {
            case ActionDefinition.TargetType.Self:
                Submit(action, new List<UnitState> { _pendingActor });
                break;

            case ActionDefinition.TargetType.AllEnemies:
                Submit(action, state.GetEnemiesOf(_pendingActor).ToList());
                break;

            case ActionDefinition.TargetType.AllAllies:
                Submit(action, state.GetAlliesOf(_pendingActor).ToList());
                break;

            case ActionDefinition.TargetType.SingleEnemy:
                EnterTargetingMode(action, state.GetEnemiesOf(_pendingActor));
                break;

            case ActionDefinition.TargetType.SingleAlly:
                EnterTargetingMode(action, state.GetAlliesOf(_pendingActor));
                break;
        }
    }

    private void EnterTargetingMode(ActionDefinition action, IEnumerable<UnitState> candidates)
    {
        _pendingAction = action;

        ClearActionButtons();

        if (cancelTargetingButton != null)
            cancelTargetingButton.gameObject.SetActive(true);

        var candidateSet = new HashSet<string>(candidates.Select(c => c.UnitId));

        foreach (var view in _unitViews)
        {
            if (view.Unit.IsAlive && candidateSet.Contains(view.Unit.UnitId))
                view.SetTargetable(true, OnTargetChosen);
        }
    }

    private void OnTargetChosen(UnitState target)
    {
        if (_pendingAction == null) return;

        var action = _pendingAction;
        _pendingAction = null;
        ClearTargeting();

        Submit(action, new List<UnitState> { target });
    }

    private void CancelTargeting()
    {
        if (_pendingActor == null) return;

        _pendingAction = null;
        ClearTargeting();

        var actions = battleController.State != null
            ? _pendingActor.Definition.Actions
                .Where(a => battleController.State.ActiveUnit == _pendingActor)
                .ToList()
            : new List<ActionDefinition>();

        // Re-request available actions through the controller event
        if (battleController.State != null)
        {
            var rules = new CombatRules();
            ShowActionButtons(rules.GetAvailableActions(battleController.State, _pendingActor));
        }
    }

    private void Submit(ActionDefinition action, List<UnitState> targets)
    {
        ClearActionButtons();
        ClearTargeting();

        if (cancelTargetingButton != null)
            cancelTargetingButton.gameObject.SetActive(false);

        battleController.SubmitAction(new ActionExecution(_pendingActor, action, targets));
    }

    private void ClearActionButtons()
    {
        foreach (Transform child in actionButtonRoot)
            Destroy(child.gameObject);
    }

    private void ClearTargeting()
    {
        foreach (var view in _unitViews)
            view.SetTargetable(false);
    }

    private void OnBattleEnded(string winner)
    {
        ClearActionButtons();
        ClearTargeting();
        logText.text += $"\n\n{winner} wins.";
    }
}