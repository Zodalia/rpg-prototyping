#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public sealed class DebugPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleController battleController;

    [Header("Data")]
    [SerializeField] private List<StatusDefinition> allStatuses = new();

    private GameObject _panelRoot;
    private Transform _content;

    private void OnEnable()
    {
        battleController.StateChanged += RebuildIfVisible;
    }

    private void OnDisable()
    {
        battleController.StateChanged -= RebuildIfVisible;
    }

    private void Start()
    {
        BuildPanelShell();
        _panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        {
            bool show = !_panelRoot.activeSelf;
            _panelRoot.SetActive(show);
            if (show) Rebuild();
        }
    }

    // ───────────────────────── Shell ─────────────────────────

    private void BuildPanelShell()
    {
        // Root panel anchored to the right side of the screen
        _panelRoot = CreateObj("DebugPanelRoot", transform);
        var rt = _panelRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.offsetMin = new Vector2(-370, 0);
        rt.offsetMax = Vector2.zero;

        var bg = _panelRoot.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.05f, 0.88f);

        // ScrollRect → Viewport → Content
        var scrollObj = CreateObj("Scroll", _panelRoot.transform);
        Stretch(scrollObj.GetComponent<RectTransform>(), new RectOffset(0, 0, 0, 0));

        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewportObj = CreateObj("Viewport", scrollObj.transform);
        Stretch(viewportObj.GetComponent<RectTransform>(), new RectOffset(0, 12, 0, 0));
        viewportObj.AddComponent<Image>().color = Color.clear;
        viewportObj.AddComponent<RectMask2D>();
        scroll.viewport = viewportObj.GetComponent<RectTransform>();

        var contentObj = CreateObj("Content", viewportObj.transform);
        var contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0, 1);
        contentRt.offsetMin = new Vector2(0, 0);
        contentRt.offsetMax = new Vector2(0, 0);

        var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.spacing = 4;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRt;
        _content = contentObj.transform;

        // Scrollbar
        var barObj = CreateObj("Scrollbar", scrollObj.transform);
        var barRt = barObj.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(1, 0);
        barRt.anchorMax = new Vector2(1, 1);
        barRt.pivot = new Vector2(1, 0.5f);
        barRt.sizeDelta = new Vector2(10, 0);
        barRt.anchoredPosition = Vector2.zero;

        barObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        var handleObj = CreateObj("Handle", barObj.transform);
        Stretch(handleObj.GetComponent<RectTransform>(), new RectOffset(0, 0, 0, 0));
        handleObj.AddComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        var scrollbar = barObj.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleObj.GetComponent<RectTransform>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
    }

    // ───────────────────────── Rebuild ─────────────────────────

    private void RebuildIfVisible()
    {
        if (_panelRoot != null && _panelRoot.activeSelf)
            Rebuild();
    }

    private void Rebuild()
    {
        if (battleController.State == null) return;

        ClearContent();

        AddHeader("Debug Panel (F1)");
        AddBattleFlowSection();
        AddGlobalResourceSection();

        foreach (var unit in battleController.State.Units)
            AddUnitSection(unit);
    }

    private void ClearContent()
    {
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);
    }

    // ───────────────────── Battle Flow ─────────────────────

    private void AddBattleFlowSection()
    {
        var state = battleController.State;
        AddLabel($"Turn {state.TurnNumber}  |  Active: {state.ActiveUnit?.Definition.DisplayName ?? "—"}");

        var row = AddRow();
        AddButton(row, "Skip Turn", SkipTurn);
        AddButton(row, "Restart", () =>
        {
            if (battleController.CurrentBattle != null && battleController.CurrentParty != null)
            {
                battleController.StartBattle(battleController.CurrentBattle, battleController.CurrentParty);
                Rebuild();
            }
        });

        var row2 = AddRow();
        AddButton(row2, "Kill All Enemies", () => ForceKillTeam("Enemy"));
        AddButton(row2, "Kill All Players", () => ForceKillTeam("Player"));

        AddSeparator();
    }

    private void SkipTurn()
    {
        var state = battleController.State;
        if (state == null || state.IsBattleOver || state.ActiveUnit == null) return;

        var dummy = ScriptableObject.CreateInstance<ActionDefinition>();
        var exec = new ActionExecution(state.ActiveUnit, dummy, new List<UnitState>());
        battleController.SubmitAction(exec);
        Rebuild();
    }

    private void ForceKillTeam(string team)
    {
        var state = battleController.State;
        if (state == null) return;

        foreach (var unit in state.Units.Where(u => u.Team == team && u.IsAlive))
        {
            unit.Hp = 0;
            unit.IsAlive = false;
        }

        battleController.NotifyStateChanged();
        Rebuild();
    }

    // ──────────────── Global / Team Resources ────────────────

    private void AddGlobalResourceSection()
    {
        var state = battleController.State;
        bool any = false;

        if (state.GlobalResources.Count > 0)
        {
            AddSectionHeader("Global Resources");
            foreach (var kvp in state.GlobalResources)
                AddResourceRow(kvp.Key, kvp.Value);
            any = true;
        }

        foreach (var teamKvp in state.TeamResources)
        {
            if (teamKvp.Value.Count == 0) continue;
            AddSectionHeader($"Team: {teamKvp.Key} Resources");
            foreach (var resKvp in teamKvp.Value)
                AddResourceRow(resKvp.Key, resKvp.Value);
            any = true;
        }

        if (any) AddSeparator();
    }

    // ───────────────────── Unit Sections ─────────────────────

    private void AddUnitSection(UnitState unit)
    {
        string alive = unit.IsAlive ? "" : " [DEAD]";
        AddSectionHeader($"{unit.Definition.DisplayName} ({unit.Team}){alive}");

        // HP row
        {
            var row = AddRow();
            AddSmallLabel(row, "HP");
            AddIntField(row, unit.Hp, 50, val =>
            {
                unit.Hp = Mathf.Clamp(val, 0, unit.Definition.MaxHp);
                unit.IsAlive = unit.Hp > 0;
                battleController.NotifyStateChanged();
                Rebuild();
            });
            AddSmallLabel(row, $"/ {unit.Definition.MaxHp}");
            AddButton(row, unit.IsAlive ? "Kill" : "Revive", () =>
            {
                if (unit.IsAlive)
                {
                    unit.Hp = 0;
                    unit.IsAlive = false;
                }
                else
                {
                    unit.IsAlive = true;
                    unit.Hp = 1;
                }
                battleController.NotifyStateChanged();
                Rebuild();
            });
        }

        // Resources
        foreach (var kvp in unit.Resources)
            AddResourceRow(kvp.Key, kvp.Value);

        // Active statuses
        for (int i = unit.Statuses.Count - 1; i >= 0; i--)
        {
            var status = unit.Statuses[i];
            var row = AddRow();
            AddSmallLabel(row, $"{status.Definition.DisplayName} ({status.RemainingTurns}t)");
            var captured = status;
            AddButton(row, "X", () =>
            {
                unit.Statuses.Remove(captured);
                battleController.NotifyStateChanged();
                Rebuild();
            });
        }

        // Add status controls
        if (allStatuses.Count > 0)
        {
            var addRow = AddRow();
            AddSmallLabel(addRow, "Add:");

            int selectedIndex = 0;
            int duration = allStatuses.Count > 0 ? allStatuses[0].DefaultDuration : 1;

            var dropdown = AddDropdown(addRow, allStatuses.Select(s => s.DisplayName).ToList(), 120, idx => selectedIndex = idx);

            var durationField = AddIntField(addRow, duration, 40, val => duration = Mathf.Max(1, val));

            AddButton(addRow, "+", () =>
            {
                var def = allStatuses[selectedIndex];
                var instance = new StatusInstance(def, duration, def.TickTiming, def.TrackingScope, unit.Team);
                unit.Statuses.Add(instance);
                battleController.NotifyStateChanged();
                Rebuild();
            });
        }

        AddSeparator();
    }

    private void AddResourceRow(string key, ResourceInstance res)
    {
        var row = AddRow();
        string label = res.Definition != null ? res.Definition.Id : key;
        AddSmallLabel(row, label);
        AddIntField(row, res.CurrentValue, 50, val =>
        {
            res.CurrentValue = Mathf.Max(0, val);
            battleController.NotifyStateChanged();
            Rebuild();
        });
    }

    // ───────────────────── UI Helpers ─────────────────────

    private void AddHeader(string text)
    {
        var obj = CreateObj("Header", _content);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 28;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private void AddSectionHeader(string text)
    {
        var obj = CreateObj("Section", _content);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 22;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 13;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.9f, 0.8f, 0.3f);
        tmp.alignment = TextAlignmentOptions.Left;
    }

    private void AddLabel(string text)
    {
        var obj = CreateObj("Label", _content);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 20;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 12;
        tmp.color = new Color(0.8f, 0.8f, 0.8f);
        tmp.alignment = TextAlignmentOptions.Left;
    }

    private void AddSeparator()
    {
        var obj = CreateObj("Sep", _content);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 6;
    }

    private GameObject AddRow()
    {
        var obj = CreateObj("Row", _content);
        var hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.spacing = 4;

        var le = obj.AddComponent<LayoutElement>();
        le.preferredHeight = 28;

        return obj;
    }

    private void AddSmallLabel(GameObject parent, string text)
    {
        var obj = CreateObj("Lbl", parent.transform);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = text.Length * 7 + 10;
        le.preferredHeight = 28;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 12;
        tmp.color = new Color(0.8f, 0.8f, 0.8f);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void AddButton(GameObject parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var obj = CreateObj("Btn", parent.transform);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = Mathf.Max(30, label.Length * 9 + 16);
        le.preferredHeight = 28;

        var img = obj.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var txtObj = CreateObj("Text", obj.transform);
        Stretch(txtObj.GetComponent<RectTransform>(), new RectOffset(2, 2, 2, 2));
        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 11;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private TMP_InputField AddIntField(GameObject parent, int value, float width, System.Action<int> onChange)
    {
        var obj = CreateObj("IntField", parent.transform);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 28;

        var img = obj.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var textArea = CreateObj("TextArea", obj.transform);
        Stretch(textArea.GetComponent<RectTransform>(), new RectOffset(4, 4, 2, 2));
        textArea.AddComponent<RectMask2D>();

        var txtObj = CreateObj("Text", textArea.transform);
        Stretch(txtObj.GetComponent<RectTransform>(), new RectOffset(0, 0, 0, 0));
        var tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = value.ToString();
        tmp.fontSize = 12;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        var field = obj.AddComponent<TMP_InputField>();
        field.textComponent = tmp;
        field.textViewport = textArea.GetComponent<RectTransform>();
        field.contentType = TMP_InputField.ContentType.IntegerNumber;
        field.text = value.ToString();
        field.onEndEdit.AddListener(str =>
        {
            if (int.TryParse(str, out int result))
                onChange(result);
        });

        return field;
    }

    private TMP_Dropdown AddDropdown(GameObject parent, List<string> options, float width, System.Action<int> onChanged)
    {
        var obj = CreateObj("Dropdown", parent.transform);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = 28;

        var img = obj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Caption
        var captionObj = CreateObj("Caption", obj.transform);
        Stretch(captionObj.GetComponent<RectTransform>(), new RectOffset(4, 20, 2, 2));
        var captionTmp = captionObj.AddComponent<TextMeshProUGUI>();
        captionTmp.fontSize = 11;
        captionTmp.color = Color.white;
        captionTmp.alignment = TextAlignmentOptions.Left;

        // Template
        var templateObj = CreateObj("Template", obj.transform);
        var templateRt = templateObj.GetComponent<RectTransform>();
        templateRt.anchorMin = new Vector2(0, 0);
        templateRt.anchorMax = new Vector2(1, 0);
        templateRt.pivot = new Vector2(0.5f, 1f);
        templateRt.sizeDelta = new Vector2(0, 150);
        templateObj.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
        var scrollRect = templateObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        var viewportObj = CreateObj("Viewport", templateObj.transform);
        Stretch(viewportObj.GetComponent<RectTransform>(), new RectOffset(0, 0, 0, 0));
        viewportObj.AddComponent<Image>().color = Color.clear;
        viewportObj.AddComponent<RectMask2D>();
        scrollRect.viewport = viewportObj.GetComponent<RectTransform>();

        var contentObj = CreateObj("Content", viewportObj.transform);
        var contentRt = contentObj.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0, 1);
        contentRt.sizeDelta = new Vector2(0, 28);
        scrollRect.content = contentRt;

        // Item template
        var itemObj = CreateObj("Item", contentObj.transform);
        Stretch(itemObj.GetComponent<RectTransform>(), new RectOffset(0, 0, 0, 0));
        itemObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
        var toggle = itemObj.AddComponent<Toggle>();

        var itemLabelObj = CreateObj("Item Label", itemObj.transform);
        Stretch(itemLabelObj.GetComponent<RectTransform>(), new RectOffset(4, 4, 2, 2));
        var itemTmp = itemLabelObj.AddComponent<TextMeshProUGUI>();
        itemTmp.fontSize = 11;
        itemTmp.color = Color.white;
        itemTmp.alignment = TextAlignmentOptions.Left;

        templateObj.SetActive(false);

        // Dropdown component
        var dd = obj.AddComponent<TMP_Dropdown>();
        dd.template = templateRt;
        dd.captionText = captionTmp;
        dd.itemText = itemTmp;
        dd.ClearOptions();
        dd.AddOptions(options);

        int selected = 0;
        dd.onValueChanged.AddListener(idx =>
        {
            selected = idx;
            onChanged(idx);
        });

        return dd;
    }

    // ───────────────────── Primitives ─────────────────────

    private static GameObject CreateObj(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void Stretch(RectTransform rt, RectOffset padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding.left, padding.bottom);
        rt.offsetMax = new Vector2(-padding.right, -padding.top);
    }
}

#endif
