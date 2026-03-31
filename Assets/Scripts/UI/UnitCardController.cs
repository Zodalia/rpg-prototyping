using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UnitCardController
{
    public VisualElement Root { get; }
    public UnitState Unit { get; private set; }

    private readonly Label _nameLabel;
    private readonly ProgressBar _hpBar;
    private readonly VisualElement _statusContainer;
    private readonly VisualElement _resourceContainer;
    private readonly Button _targetOverlay;
    private readonly VisualTreeAsset _statusIconTemplate;
    private readonly VisualTreeAsset _resourceEntryTemplate;
    private readonly bool _showResources;

    private Action<UnitState> _onTargeted;

    public UnitCardController(
        VisualElement root,
        VisualTreeAsset statusIconTemplate,
        VisualTreeAsset resourceEntryTemplate,
        bool showResources)
    {
        Root = root;
        _statusIconTemplate = statusIconTemplate;
        _resourceEntryTemplate = resourceEntryTemplate;
        _showResources = showResources;

        _nameLabel = root.Q<Label>("unit-name");
        _hpBar = root.Q<ProgressBar>("hp-bar");
        _statusContainer = root.Q("status-container");
        _resourceContainer = root.Q("resource-container");
        _targetOverlay = root.Q<Button>("target-overlay");
    }

    public void Bind(UnitState unit)
    {
        Unit = unit;
        _targetOverlay.clicked += () => _onTargeted?.Invoke(Unit);
        SetTargetable(false);
        Refresh(null);
    }

    public void Refresh(UnitState activeUnit)
    {
        if (Unit == null) return;

        _nameLabel.text = Unit.Definition.DisplayName;
        _hpBar.title = $"{Unit.Hp} / {Unit.Definition.MaxHp}";
        _hpBar.highValue = Unit.Definition.MaxHp;
        _hpBar.value = Unit.Hp;

        RefreshStatuses();
        RefreshResources();
        RefreshStateClasses(activeUnit);
    }

    private void RefreshStatuses()
    {
        _statusContainer.Clear();

        foreach (var status in Unit.Statuses)
        {
            var el = _statusIconTemplate.CloneTree();
            var icon = el.Q("icon");
            if (status.Definition.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(status.Definition.Icon);
                icon.style.unityBackgroundImageTintColor = (Color)status.Definition.IconColor;
            }
            el.Q<Label>("turns").text = status.RemainingTurns.ToString();
            _statusContainer.Add(el);
        }
    }

    private void RefreshResources()
    {
        _resourceContainer.Clear();

        if (!_showResources || Unit.Resources.Count == 0)
        {
            _resourceContainer.AddToClassList("hidden");
            return;
        }

        _resourceContainer.RemoveFromClassList("hidden");

        foreach (var kvp in Unit.Resources.Where(
            kvp => kvp.Value.Definition != null && kvp.Value.Definition.PlayerFacing))
        {
            var el = _resourceEntryTemplate.CloneTree();
            var icon = el.Q("icon");
            if (kvp.Value.Definition.Icon != null)
            {
                icon.style.backgroundImage = new StyleBackground(kvp.Value.Definition.Icon);
                icon.style.unityBackgroundImageTintColor = (Color)kvp.Value.Definition.IconColor;
            }
            el.Q<Label>("value").text = kvp.Value.CurrentValue.ToString();
            _resourceContainer.Add(el);
        }
    }

    private void RefreshStateClasses(UnitState activeUnit)
    {
        Root.RemoveFromClassList("unit--active");
        Root.RemoveFromClassList("unit--targetable");
        Root.RemoveFromClassList("unit--dead");

        if (!Unit.IsAlive)
            Root.AddToClassList("unit--dead");
        else if (activeUnit != null && Unit == activeUnit)
            Root.AddToClassList("unit--active");
    }

    public void SetTargetable(bool targetable, Action<UnitState> onTargeted = null)
    {
        _onTargeted = onTargeted;

        if (targetable && Unit != null && Unit.IsAlive)
        {
            _targetOverlay.RemoveFromClassList("hidden");
            Root.AddToClassList("unit--targetable");
        }
        else
        {
            _targetOverlay.AddToClassList("hidden");
            Root.RemoveFromClassList("unit--targetable");
        }
    }
}
