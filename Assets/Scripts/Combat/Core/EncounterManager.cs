using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EncounterManager : MonoBehaviour
{
    [SerializeField] private EncounterSettings settings;
    [SerializeField] private BattleController battleController;

    public EncounterSettings Settings => settings;

    public event Action<List<BattleDefinition>> ShowBattleMenu;
    public event Action BattleStarting;

    private bool _started;

    private void OnEnable()
    {
        battleController.BattleEnded += OnBattleEnded;
    }

    private void Start()
    {
        Begin();
    }

    private void OnDisable()
    {
        if (battleController != null)
            battleController.BattleEnded -= OnBattleEnded;
    }

    public void Begin()
    {
        if (_started) return;
        _started = true;

        switch (settings.Mode)
        {
            case EncounterMode.Menu:
                RequestBattleMenu();
                break;
            case EncounterMode.Random:
                StartRandomBattle();
                break;
        }
    }

    public void SelectBattle(BattleDefinition battle)
    {
        BattleStarting?.Invoke();
        battleController.StartBattle(battle, settings.PlayerParty);
    }

    public void StartRandomBattle()
    {
        if (settings.AvailableBattles.Count == 0)
        {
            Debug.LogWarning("EncounterManager: No battles available.");
            return;
        }

        int index = UnityEngine.Random.Range(0, settings.AvailableBattles.Count);
        SelectBattle(settings.AvailableBattles[index]);
    }

    private void RequestBattleMenu()
    {
        ShowBattleMenu?.Invoke(settings.AvailableBattles);
    }

    private void OnBattleEnded(string winner)
    {
        switch (settings.Mode)
        {
            case EncounterMode.Menu:
                RequestBattleMenu();
                break;
            case EncounterMode.Random:
                StartRandomBattle();
                break;
        }
    }
}
