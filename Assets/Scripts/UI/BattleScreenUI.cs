using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class BattleScreenUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleController battleController;
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private EncounterManager encounterManager;

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset unitCardTemplate;
    [SerializeField] private VisualTreeAsset actionButtonTemplate;
    [SerializeField] private VisualTreeAsset statusIconTemplate;
    [SerializeField] private VisualTreeAsset resourceEntryTemplate;
    [SerializeField] private VisualTreeAsset battleSelectTemplate;
    [SerializeField] private VisualTreeAsset poolCardTemplate;

    [Header("Options")]
    [SerializeField] private bool showUnitResources;

    [Header("Turn Tracker")]
    [SerializeField] private VisualTreeAsset turnTrackerEntryTemplate;
    [SerializeField] private int turnPreviewCount = 8;

    [Header("Event Pacing")]
    [SerializeField] private float groupDelay = 1f;
    [SerializeField] private float intraGroupDelay = 0.15f;

    [Header("Log")]
    [SerializeField] private int maxLogLines = 12;

    // Cached elements
    private VisualElement _root;
    private VisualElement _allyColumn;
    private VisualElement _enemyColumn;
    private VisualElement _actionBar;
    private Button _cancelButton;
    private ScrollView _combatLog;
    private VisualElement _combatLogContent;
    private VisualElement _resourceBar;
    private VisualElement _allyTeamResources;
    private VisualElement _enemyTeamResources;
    private VisualElement _turnTracker;
    private VisualElement _previewPanel;
    private VisualElement _poolContainer;

    // State
    private readonly List<UnitCardController> _unitCards = new();
    private readonly List<(PoolInstance pool, VisualElement element)> _poolCards = new();
    private UnitState _pendingActor;
    private ActionDefinition _pendingAction;

    private readonly Queue<BattleEventGroup> _pendingGroups = new();
    private readonly List<string> _logLines = new();
    private bool _isProcessingLog;
    private Coroutine _processingCoroutine;
    private UnitState _deferredActor;
    private List<ActionDefinition> _deferredActions;
    private BattleEventBus _subscribedBus;
    private List<TurnPreviewEntry> _baselineTurnPreview = new();

    // Battle selection
    private VisualElement _battleRoot;
    private VisualElement _selectRoot;
    private VisualElement _battleListContent;
    private Button _randomButton;

    private void OnEnable()
    {
        _root = uiDocument.rootVisualElement;
        CacheElements();

        battleController.StateChanged += OnStateChanged;
        battleController.PlayerInputRequested += OnPlayerInputRequested;
        battleController.BattleEnded += OnBattleEnded;

        _cancelButton.clicked += CancelTargeting;
        _cancelButton.AddToClassList("hidden");

        if (encounterManager != null)
        {
            encounterManager.ShowBattleMenu += OnShowBattleMenu;
            encounterManager.BattleStarting += OnBattleStarting;

            // Hide battle screen until a battle is selected
            if (_battleRoot != null)
                _battleRoot.style.display = DisplayStyle.None;

            // Kick off encounter flow if it hasn't started yet
            encounterManager.Begin();
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

        _cancelButton.clicked -= CancelTargeting;

        if (encounterManager != null)
        {
            encounterManager.ShowBattleMenu -= OnShowBattleMenu;
            encounterManager.BattleStarting -= OnBattleStarting;
        }
    }

    private void CacheElements()
    {
        _battleRoot = _root.Q("battle-root");
        _allyColumn = _root.Q("ally-column");
        _enemyColumn = _root.Q("enemy-column");
        _actionBar = _root.Q("action-bar");
        _cancelButton = _root.Q<Button>("cancel-targeting-button");
        _combatLog = _root.Q<ScrollView>("combat-log");
        _combatLogContent = _root.Q("combat-log-content");
        _resourceBar = _root.Q("resource-bar");
        _allyTeamResources = _root.Q("ally-team-resources");
        _enemyTeamResources = _root.Q("enemy-team-resources");
        _turnTracker = _root.Q("turn-tracker");
        _poolContainer = _root.Q("pool-container");

        _previewPanel = new VisualElement();
        _previewPanel.AddToClassList("action-preview-panel");
        _previewPanel.AddToClassList("hidden");
        _previewPanel.pickingMode = PickingMode.Ignore;
        _root.Add(_previewPanel);
    }

    // ─────────────────────── Event Handlers ───────────────────────

    private void OnStateChanged()
    {
        if (battleController.State == null)
            return;

        SubscribeToEventBus();
        RebuildUnitCardsIfNewBattle();
        SpawnUnitCardsIfNeeded();
        RefreshAll();

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

    // ─────────────────────── Log Processing ───────────────────────

    private IEnumerator ProcessLog()
    {
        _isProcessingLog = true;
        yield return null;

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
                        RefreshAll();
                        RefreshLog();

                        if (i < group.Events.Count - 1)
                            yield return new WaitForSeconds(intraGroupDelay);
                    }
                }

                if (_pendingGroups.Count > 0)
                    yield return new WaitForSeconds(groupDelay);
                else
                    yield return new WaitForSeconds(groupDelay);
            }

            if (_deferredActor != null)
                break;

            if (battleController.State.IsBattleOver)
                break;

            battleController.AdvanceTurn();
            yield return null;

            if (battleController.State.IsBattleOver)
                break;

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
            ResourceChangedEvent resourceChanged =>
                resourceChanged.Unit != null
                    ? $"{resourceChanged.Unit.Definition.DisplayName} {(resourceChanged.NewValue > resourceChanged.OldValue ? "gains" : "loses")} {Mathf.Abs(resourceChanged.NewValue - resourceChanged.OldValue)} {resourceChanged.Resource.DisplayName}"
                    : $"{(resourceChanged.NewValue > resourceChanged.OldValue ? "Gains" : "Loses")} {Mathf.Abs(resourceChanged.NewValue - resourceChanged.OldValue)} {resourceChanged.Resource.DisplayName}",
            PoolHarvestedEvent poolHarvest =>
                $"{poolHarvest.Actor.Definition.DisplayName} harvests {poolHarvest.Amount} {poolHarvest.Resource.DisplayName} from {poolHarvest.Pool.Definition.DisplayName}",
            PoolDepletedEvent poolDepleted =>
                $"{poolDepleted.Pool.Definition.DisplayName} is depleted",
            PoolSpawnedEvent poolSpawned =>
                $"{poolSpawned.Pool.Definition.DisplayName} appears",
            _ => null
        };
    }

    // ─────────────────────── Refresh ──────────────────────────────

    private void RefreshAll()
    {
        var activeUnit = battleController.State.ActiveUnit;
        foreach (var card in _unitCards)
            card.Refresh(activeUnit);

        RefreshResourceBar();
        RefreshPoolCards();

        _baselineTurnPreview = ComputeTurnPreview(null);
        RefreshTurnTracker(null);
    }

    private void RefreshLog()
    {
        _combatLogContent.Clear();

        foreach (var line in _logLines.TakeLast(maxLogLines))
        {
            var label = new Label(line);
            label.AddToClassList("log-entry");
            _combatLogContent.Add(label);
        }

        _combatLog.schedule.Execute(() =>
            _combatLog.scrollOffset = new Vector2(0, float.MaxValue));
    }

    private void RefreshResourceBar()
    {
        _resourceBar.Clear();
        _allyTeamResources.Clear();
        _enemyTeamResources.Clear();

        var state = battleController.State;
        if (state == null) return;

        foreach (var kvp in state.GlobalResources.Where(
            kvp => kvp.Value.Definition != null && kvp.Value.Definition.PlayerFacing))
        {
            SpawnResourceEntry(_resourceBar, kvp.Value);
        }

        if (_resourceBar.childCount > 0)
            _resourceBar.RemoveFromClassList("hidden");
        else
            _resourceBar.AddToClassList("hidden");

        foreach (var teamKvp in state.TeamResources)
        {
            var container = teamKvp.Key == "Player" ? _allyTeamResources : _enemyTeamResources;
            foreach (var resKvp in teamKvp.Value.Where(
                kvp => kvp.Value.Definition != null && kvp.Value.Definition.PlayerFacing))
            {
                SpawnResourceEntry(container, resKvp.Value);
            }
        }

        ToggleHidden(_allyTeamResources, _allyTeamResources.childCount == 0);
        ToggleHidden(_enemyTeamResources, _enemyTeamResources.childCount == 0);
    }

    private static void ToggleHidden(VisualElement el, bool hidden)
    {
        if (hidden)
            el.AddToClassList("hidden");
        else
            el.RemoveFromClassList("hidden");
    }

    private void SpawnResourceEntry(VisualElement parent, ResourceInstance resource)
    {
        var el = resourceEntryTemplate.CloneTree();
        var icon = el.Q("icon");
        if (resource.Definition != null && resource.Definition.Icon != null)
        {
            icon.style.backgroundImage = new StyleBackground(resource.Definition.Icon);
            icon.style.unityBackgroundImageTintColor = (Color)resource.Definition.IconColor;
        }
        el.Q<Label>("value").text = resource.CurrentValue.ToString();
        parent.Add(el);
    }

    // ─────────────────────── Pool Cards ───────────────────────────

    private void RefreshPoolCards()
    {
        var state = battleController.State;
        if (state == null || poolCardTemplate == null) return;

        // Rebuild pool cards if count changed (spawns/new battle)
        if (_poolCards.Count != state.Pools.Count)
        {
            _poolContainer.Clear();
            _poolCards.Clear();

            foreach (var pool in state.Pools)
            {
                var tree = poolCardTemplate.CloneTree();
                var card = tree.Q(className: "pool-card");

                card.Q<Label>("pool-name").text = pool.Definition.DisplayName;

                var icon = card.Q("pool-icon");
                if (pool.Definition.Icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(pool.Definition.Icon);
                    icon.style.unityBackgroundImageTintColor = (Color)pool.Definition.IconColor;
                }

                _poolContainer.Add(tree);
                _poolCards.Add((pool, card));
            }
        }

        // Refresh state
        foreach (var (pool, card) in _poolCards)
        {
            if (pool.Definition.MaxHarvests >= 0)
                card.Q<Label>("pool-harvests").text = $"{pool.RemainingHarvests} left";
            else
                card.Q<Label>("pool-harvests").text = "\u221E"; // ∞

            card.EnableInClassList("pool--depleted", pool.IsDepleted);
        }

        _poolContainer.EnableInClassList("hidden", state.Pools.Count == 0);
    }

    // ─────────────────────── Turn Tracker ─────────────────────────

    private List<TurnPreviewEntry> ComputeTurnPreview(ActionDefinition hoverAction)
    {
        var state = battleController.State;
        if (state == null) return new List<TurnPreviewEntry>();

        var strategy = battleController.TurnOrderStrategy;
        if (strategy != null)
        {
            return strategy.GetTurnPreview(
                state, battleController.EnemyAi, turnPreviewCount,
                battleController.GlobalTurnEffects,
                hoverAction, _pendingActor);
        }

        return new CombatRules().GetTurnPreview(state, turnPreviewCount);
    }

    private void RefreshTurnTracker(ActionDefinition hoverAction)
    {
        _turnTracker.Clear();

        var preview = hoverAction != null
            ? ComputeTurnPreview(hoverAction)
            : _baselineTurnPreview;

        for (int i = 0; i < preview.Count; i++)
        {
            var previewEntry = preview[i];
            var unit = previewEntry.Unit;
            var el = turnTrackerEntryTemplate.CloneTree();
            var entry = el.Q(className: "turn-entry");

            el.Q<Label>("turn-entry-name").text = unit.Definition.DisplayName;
            el.Q<Label>("turn-entry-delay").text = previewEntry.ResourceValue.ToString();

            if (i == 0)
                entry.AddToClassList("turn-entry--current");

            entry.AddToClassList(unit.Team == "Player"
                ? "turn-entry--player"
                : "turn-entry--enemy");

            bool isSpeculative = hoverAction != null &&
                (i >= _baselineTurnPreview.Count || _baselineTurnPreview[i].Unit != unit);
            if (isSpeculative)
                entry.AddToClassList("turn-entry--speculative");

            _turnTracker.Add(el);
        }
    }

    // ─────────────────────── Unit Cards ───────────────────────────

    private void SpawnUnitCardsIfNeeded()
    {
        if (_unitCards.Count > 0)
            return;

        foreach (var unit in battleController.State.Units)
        {
            var parent = unit.Team == "Player" ? _allyColumn : _enemyColumn;
            var tree = unitCardTemplate.CloneTree();
            var cardRoot = tree.Q(className: "unit-card");

            var controller = new UnitCardController(
                cardRoot, statusIconTemplate, resourceEntryTemplate, showUnitResources);
            controller.Bind(unit);

            parent.Add(tree);
            _unitCards.Add(controller);
        }
    }

    private void RebuildUnitCardsIfNewBattle()
    {
        if (_unitCards.Count == 0)
            return;

        // If the first tracked unit isn't in the current state, it's a new battle
        if (battleController.State.Units.Contains(_unitCards[0].Unit))
            return;

        _unitCards.Clear();
        _poolCards.Clear();
        _allyColumn.Clear();
        _enemyColumn.Clear();
        _poolContainer.Clear();
        _pendingGroups.Clear();
        _logLines.Clear();
        _combatLogContent.Clear();
        _pendingActor = null;
        _pendingAction = null;
        _deferredActor = null;
        _deferredActions = null;
        ClearActionButtons();
    }

    // ─────────────────────── Actions ──────────────────────────────

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
        _cancelButton.AddToClassList("hidden");

        foreach (var action in actions)
        {
            var tree = actionButtonTemplate.CloneTree();
            var button = tree.Q<Button>("action-button");
            button.Q<Label>("label").text = action.DisplayName;

            var costContainer = button.Q("cost-container");
            foreach (var req in action.ResourceRequirements)
            {
                if (req.Amount <= 0) continue;
                if (!req.IsTagBased && req.Resource == null) continue;

                var entry = resourceEntryTemplate.CloneTree();
                var icon = entry.Q("icon");

                if (req.IsTagBased)
                {
                    // Tag-based: no icon, just show tag name + amount
                    entry.Q<Label>("value").text = $"{req.Amount} {req.Tag.DisplayName}";
                }
                else
                {
                    if (req.Resource.Icon != null)
                    {
                        icon.style.backgroundImage = new StyleBackground(req.Resource.Icon);
                        icon.style.unityBackgroundImageTintColor = (Color)req.Resource.IconColor;
                    }
                    entry.Q<Label>("value").text = req.Amount.ToString();
                }
                costContainer.Add(entry);
            }

            var captured = action;
            button.clicked += () => OnActionChosen(captured);
            button.RegisterCallback<MouseEnterEvent>(_ =>
            {
                RefreshTurnTracker(captured);
                ShowPreview(captured, button);
            });
            button.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                RefreshTurnTracker(null);
                HidePreview();
            });

            _actionBar.Add(tree);
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

            case ActionDefinition.TargetType.Pool:
                EnterPoolTargetingMode(action);
                break;
        }
    }

    // ─────────────────────── Targeting ─────────────────────────────

    private void EnterTargetingMode(ActionDefinition action, IEnumerable<UnitState> candidates)
    {
        _pendingAction = action;
        ClearActionButtons();
        _cancelButton.RemoveFromClassList("hidden");

        var candidateSet = new HashSet<string>(candidates.Select(c => c.UnitId));

        foreach (var card in _unitCards)
        {
            if (card.Unit.IsAlive && candidateSet.Contains(card.Unit.UnitId))
                card.SetTargetable(true, OnTargetChosen);
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

    private void EnterPoolTargetingMode(ActionDefinition action)
    {
        _pendingAction = action;
        ClearActionButtons();
        _cancelButton.RemoveFromClassList("hidden");

        foreach (var (pool, card) in _poolCards)
        {
            if (pool.IsDepleted) continue;

            card.AddToClassList("pool--targetable");
            var overlay = card.Q<Button>("pool-target-overlay");
            overlay.RemoveFromClassList("hidden");

            var capturedPool = pool;
            overlay.clicked += () => OnPoolChosen(capturedPool);
        }
    }

    private void OnPoolChosen(PoolInstance pool)
    {
        if (_pendingAction == null) return;

        var action = _pendingAction;
        _pendingAction = null;
        ClearTargeting();

        ClearActionButtons();
        _cancelButton.AddToClassList("hidden");

        battleController.SubmitAction(new ActionExecution(_pendingActor, action, pool));
    }

    private void CancelTargeting()
    {
        if (_pendingActor == null) return;

        _pendingAction = null;
        ClearTargeting();

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
        _cancelButton.AddToClassList("hidden");

        battleController.SubmitAction(new ActionExecution(_pendingActor, action, targets));
    }

    private void ClearActionButtons()
    {
        HidePreview();

        // Remove all action buttons but keep the cancel button
        var toRemove = new List<VisualElement>();
        foreach (var child in _actionBar.Children())
        {
            if (child != _cancelButton)
                toRemove.Add(child);
        }
        foreach (var el in toRemove)
            _actionBar.Remove(el);
    }

    private void ShowPreview(ActionDefinition action, VisualElement button)
    {
        if (_pendingActor == null || battleController.State == null)
            return;

        var lines = ActionPreviewSimulator.Simulate(battleController.State, _pendingActor, action);
        if (lines.Count == 0)
        {
            HidePreview();
            return;
        }

        _previewPanel.Clear();
        foreach (var line in lines)
        {
            var label = new Label(line);
            label.AddToClassList("action-preview-line");
            label.pickingMode = PickingMode.Ignore;
            _previewPanel.Add(label);
        }

        _previewPanel.RemoveFromClassList("hidden");

        // Position to the right of the hovered button
        _previewPanel.schedule.Execute(() =>
        {
            var btnRect = button.worldBound;
            var rootRect = _root.worldBound;
            _previewPanel.style.left = btnRect.xMax - rootRect.x + 8;
            _previewPanel.style.top = btnRect.y - rootRect.y;
        });
    }

    private void HidePreview()
    {
        _previewPanel.AddToClassList("hidden");
        _previewPanel.Clear();
    }

    private void ClearTargeting()
    {
        foreach (var card in _unitCards)
            card.SetTargetable(false);

        foreach (var (_, card) in _poolCards)
        {
            card.RemoveFromClassList("pool--targetable");
            var overlay = card.Q<Button>("pool-target-overlay");
            overlay.AddToClassList("hidden");
            overlay.clickable = new Clickable(() => { });
        }
    }

    private void OnBattleEnded(string winner)
    {
        ClearActionButtons();
        ClearTargeting();

        var endLabel = new Label($"\n{winner} wins.");
        endLabel.AddToClassList("log-entry");
        _combatLogContent.Add(endLabel);
    }

    // ─────────────────── Battle Selection ──────────────────────

    private void OnShowBattleMenu(List<BattleDefinition> battles)
    {
        EnsureSelectUI();
        _selectRoot.style.display = DisplayStyle.Flex;
        _battleRoot.style.display = DisplayStyle.None;

        _battleListContent.Clear();

        foreach (var battle in battles)
        {
            var button = new Button(() => encounterManager.SelectBattle(battle))
            {
                text = battle.DisplayName,
            };
            button.AddToClassList("battle-select-button");
            _battleListContent.Add(button);
        }
    }

    private void OnBattleStarting()
    {
        if (_selectRoot != null)
            _selectRoot.style.display = DisplayStyle.None;

        _battleRoot.style.display = DisplayStyle.Flex;
    }

    private void EnsureSelectUI()
    {
        if (_selectRoot != null) return;

        var template = battleSelectTemplate.Instantiate();
        _selectRoot = template.Q("battle-select-root");
        _battleListContent = template.Q("battle-list-content");
        _randomButton = template.Q<Button>("random-battle-button");
        _randomButton.clicked += () => encounterManager.StartRandomBattle();

        _root.Add(template);
    }
}
