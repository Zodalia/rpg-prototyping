using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UnitView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI hpLabel;
    [SerializeField] private Slider hpBar;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private Button targetButton;
    [SerializeField] private Image backgroundImage;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color activeColor = new Color(0.1f, 0.4f, 0.1f, 0.9f);
    [SerializeField] private Color targetableColor = new Color(0.4f, 0.3f, 0.1f, 0.9f);
    [SerializeField] private Color deadColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);

    public UnitState Unit { get; private set; }

    private Action<UnitState> _onTargeted;

    public void Bind(UnitState unit)
    {
        Unit = unit;
        targetButton.onClick.RemoveAllListeners();
        targetButton.onClick.AddListener(() => _onTargeted?.Invoke(Unit));
        SetTargetable(false);
        Refresh(null);
    }

    public void Refresh(UnitState activeUnit)
    {
        if (Unit == null) return;

        nameLabel.text = Unit.Definition.DisplayName;
        hpLabel.text = $"{Unit.Hp} / {Unit.Definition.MaxHp}";
        hpBar.maxValue = Unit.Definition.MaxHp;
        hpBar.value = Unit.Hp;

        if (Unit.Statuses.Count > 0)
            statusLabel.text = string.Join(", ", Unit.Statuses.Select(s => $"{s.Definition.DisplayName}({s.RemainingTurns})"));
        else
            statusLabel.text = "";

        if (!Unit.IsAlive)
        {
            backgroundImage.color = deadColor;
            nameLabel.alpha = 0.5f;
        }
        else if (activeUnit != null && Unit == activeUnit)
        {
            backgroundImage.color = activeColor;
            nameLabel.alpha = 1f;
        }
        else
        {
            backgroundImage.color = normalColor;
            nameLabel.alpha = 1f;
        }
    }

    public void SetTargetable(bool targetable, Action<UnitState> onTargeted = null)
    {
        _onTargeted = onTargeted;
        targetButton.interactable = targetable;
        targetButton.gameObject.SetActive(targetable);

        if (targetable && Unit != null && Unit.IsAlive)
            backgroundImage.color = targetableColor;
    }
}