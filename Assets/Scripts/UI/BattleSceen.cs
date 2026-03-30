using System.Collections;
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
    [SerializeField] private int maxLogLines = 12;

    [Header("Resources")]
    [SerializeField] private ResourceBarView resourceBarView;

    [Header("Event Pacing")]
    [SerializeField] private float groupDelay = 1f;
    [SerializeField] private float intraGroupDelay = 0.15f;

    private readonly List<UnitView> _unitViews = new();
    private UnitState _pendingActor;
    private ActionDefinition _pendingAction;

    private readonly Queue<BattleEventGroup> _pendingGroups = new();
    private readonly List<string> _logLines = new();
    private bool _isProcessingLog;
    private Coroutine _processingCoroutine;
    private UnitState _deferredActor;
    private List<ActionDefinition> _deferredActions;
    private BattleEventBus _subscribedBus;

    private void OnEnable()
    {
        battleController.StateChanged += OnStateChanged;
        battleController.PlayerInputRequested += OnPlayerInputRequested;
        battleController.BattleEnded += OnBattleEnded;

        if (cancelTargetingButton != null)
        {
            cancelTargetingButton.onClick.AddListener(CancelTargeting);
            cancelTargetingButton.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        battleController.StateChanged -= OnStateChanged;
        battleController.PlayerInputRequested -= OnPlayerInputRequested;
        battleController.BattleEnded -= OnBattleEnded;

        if (_subscribedBus != null)
        {
            _subscribedBus.GroupRaised -= OnGroupRaised;
            _subscribedBus = null;
        }

        if (cancelTargetingButton != null)
            cancelTargetingButton.onClick.RemoveListener(CancelTargeting);
    }

    private void OnStateChanged()
    {
        if (battleController.State == null)
            return;

        SubscribeToEventBus();
        SpawnUnitViewsIfNeeded();
        RefreshUnitViews();

        if (!_isProcessingLog && _pendingGroups.Count > 0)
            _processingCoroutine = StartCoroutine(ProcessLog());
    }

    private void SubscribeToEventBus()
    {
        var bus = battleController.State.EventBus;
        if (bus == _subscribedBus)
            return;

        if (_subscribedBus != null)
            _subscribedBus.GroupRaised -= OnGroupRaised;

        _subscribedBus = bus;
        _subscribedBus.GroupRaised += OnGroupRaised;
    }

    private void OnGroupRaised(BattleEventGroup group)
    {
        _pendingGroups.Enqueue(group);

        if (!_isProcessingLog)
            _processingCoroutine = StartCoroutine(ProcessLog());
    }

    private void OnPlayerInputRequested(UnitState actor, List<ActionDefinition> actions)
    {
        if (_isProcessingLog)
        {
            _deferredActor = actor;
            _deferredActions = actions;
        }
        else
        {
            ShowPlayerActions(actor, actions);
        }
    }

    private IEnumerator ProcessLog()
    {
        _isProcessingLog = true;
        yield return null; // let all synchronous events settle

        while (true)
        {
            while (_pendingGroups.Count > 0)
            {
                var group = _pendingGroups.Dequeue();

                for (int i = 0; i < group.Events.Count; i++)
                {
                    var text = FormatEvent(group.Events[i]);
                    if (text != null)
                    {
                        _logLines.Add(text);
                        RefreshUnitViews();
                        RefreshLog();

                        if (i < group.Events.Count - 1)
                            yield return new WaitForSeconds(intraGroupDelay);
                    }
                }

                // Delay between groups (skip if that was the last queued group and nothing more is coming)
                if (_pendingGroups.Count > 0)
                    yield return new WaitForSeconds(groupDelay);
                else
                    yield return new WaitForSeconds(groupDelay);
            }

            // Player input is waiting — hand off control
            if (_deferredActor != null)
                break;

            // Battle over — nothing more to do
            if (battleController.State.IsBattleOver)
                break;

            // Advance to next turn (may produce more groups from enemy actions / status ticks)
            battleController.AdvanceTurn();
            yield return null; // let synchronous events settle

            if (battleController.State.IsBattleOver)
                break;

            // No new groups — next turn is ready with nothing to show
            if (_pendingGroups.Count == 0)
                break;
        }

        _isProcessingLog = false;
        _processingCoroutine = null;

        if (_deferredActor != null)
        {
            var actor = _deferredActor;
            var actions = _deferredActions;
            _deferredActor = null;
            _deferredActions = null;
            ShowPlayerActions(actor, actions);
        }
    }

    private string FormatEvent(BattleEvent e)
    {
        return e switch
        {
            ActionUsedEvent action =>
                $"{action.Actor.Definition.DisplayName} uses {action.Action.DisplayName}",
            DamageDealtEvent damage =>
                $"{damage.Actor.Definition.DisplayName} hits {damage.Target.Definition.DisplayName} for {damage.Amount}",
            UnitDefeatedEvent defeated =>
                $"{defeated.Unit.Definition.DisplayName} is defeated",
            StatusAppliedEvent statusApplied =>
                $"{statusApplied.Target.Definition.DisplayName} gains {statusApplied.Status.DisplayName}",
            StatusTickEvent statusTick =>
                $"{statusTick.Unit.Definition.DisplayName} takes {statusTick.Damage} from {statusTick.Status.DisplayName}",
            StatusExpiredEvent statusExpired =>
                $"{statusExpired.Unit.Definition.DisplayName} loses {statusExpired.Status.DisplayName}",
            _ => null
        };
    }

    private void RefreshUnitViews()
    {
        var activeUnit = battleController.State.ActiveUnit;
        foreach (var view in _unitViews)
            view.Refresh(activeUnit);

        if (resourceBarView != null)
            resourceBarView.Refresh(battleController.State);
    }

    private void RefreshLog()
    {
        var entries = _logLines.TakeLast(maxLogLines);
        logText.text = string.Join("\n", entries);
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