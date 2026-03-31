#if LEGACY_UGUI
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
    [SerializeField] private Transform statusParent;
    [SerializeField] private GameObject statusPrefab;
    [SerializeField] private Button targetButton;
    [SerializeField] private Image backgroundImage;

    [Header("Resources")]
    [SerializeField] private Transform resourceParent;
    [SerializeField] private GameObject resourceEntryPrefab;
    [SerializeField] private bool showResources;

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

        foreach (Transform child in statusParent)
        {
            Destroy(child.gameObject);
        }

        if (Unit.Statuses.Count > 0)
        {
            foreach (var status in Unit.Statuses)
            {
                var statusView = Instantiate(statusPrefab, statusParent);
                var statusImage = statusView.GetComponent<Image>();
                if (statusImage != null)
                {
                    statusImage.sprite = status.Definition.Icon;
                    statusImage.color = status.Definition.IconColor;
                }
                var statusLabel = statusView.GetComponentInChildren<TextMeshProUGUI>();
                if (statusLabel != null)                {
                    statusLabel.text = status.RemainingTurns.ToString();
                }
            }
        }

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

        RefreshResources();
    }

    private void RefreshResources()
    {
        if (resourceParent == null || resourceEntryPrefab == null) return;

        foreach (Transform child in resourceParent)
            Destroy(child.gameObject);

        if (!showResources || Unit == null || Unit.Resources.Count == 0)
        {
            if (resourceParent != null)
                resourceParent.gameObject.SetActive(false);
            return;
        }

        resourceParent.gameObject.SetActive(true);

        foreach (var kvp in Unit.Resources.Where(kvp => kvp.Value.Definition != null && kvp.Value.Definition.PlayerFacing))
        {
            var entry = Instantiate(resourceEntryPrefab, resourceParent);
            var icon = entry.GetComponent<Image>();
            if (icon != null && kvp.Value.Definition != null)
            {
                icon.sprite = kvp.Value.Definition.Icon;
                icon.color = kvp.Value.Definition.IconColor;
            }

            var label = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {

                label.text = kvp.Value.CurrentValue.ToString();
            }
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
#endif