using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActionButtonView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    public void Bind(ActionDefinition action, Action onClick)
    {
        label.text = action.DisplayName;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}