#if LEGACY_UGUI
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceBarView : MonoBehaviour
{
    [SerializeField] private Transform resourceParent;
    [SerializeField] private GameObject resourceEntryPrefab;

    public void Refresh(BattleState state)
    {
        foreach (Transform child in resourceParent)
            Destroy(child.gameObject);

        if (state == null) return;

        foreach (var kvp in state.GlobalResources.Where(kvp => kvp.Value.Definition != null && kvp.Value.Definition.PlayerFacing))
            SpawnEntry(kvp.Value, "");

        foreach (var teamKvp in state.TeamResources.Where(kvp => kvp.Value.Values.Any(res => res.Definition != null && res.Definition.PlayerFacing)))
        {
            foreach (var resKvp in teamKvp.Value)
                SpawnEntry(resKvp.Value, teamKvp.Key);
        }

        gameObject.SetActive(resourceParent.childCount > 0);
    }

    private void SpawnEntry(ResourceInstance resource, string teamLabel)
    {
        var entry = Instantiate(resourceEntryPrefab, resourceParent);

        var icon = entry.GetComponent<Image>();
        if (icon != null && resource.Definition != null)
        {
            icon.sprite = resource.Definition.Icon;
            icon.color = resource.Definition.IconColor;
        }

        var label = entry.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = resource.CurrentValue.ToString();
        }
    }
}
#endif
