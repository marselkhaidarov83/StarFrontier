using System;
using System.IO;
using UnityEngine;

//Сервис сохранения по непрерывныым тикам
public class SaveService : ISaveService
{
    private float _autosaveIntervalSeconds = 60f;
    private const string FileName = "save.json";
    private float autosaveTimer = 0f;
    private bool hasUnsavedChanges;

    private SimpleEventBus eventBus;
    private IGameSessionService gameSessionService;
    private ISystemEncounterSaveService _systemEncounterSaveService;
    private ISystemNpcSimulationSaveService _systemNpcSimulationSaveService;

    private bool enabledSave = true;

    public SaveService()
    {
        _autosaveIntervalSeconds = Bootstrapper.Instance.AutosaveIntervalSeconds;
        eventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        _systemEncounterSaveService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemEncounterSaveService>();
        _systemNpcSimulationSaveService = Bootstrapper.Instance.ServiceRegistry.Get<ISystemNpcSimulationSaveService>();

        eventBus.Subscribe<SaveNeedEvent>(OnSaveNeedEvent);
    }

    private void OnSaveNeedEvent(SaveNeedEvent evt)
    {
        Save();
    }

    public void EnableSave(bool enable)
    {
        enabledSave = enable;
    }

    private string GetFullPath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }

    public bool HasSave()
    {
        return File.Exists(GetFullPath());
    }

    public void Save()
    {
        Save(gameSessionService?.CurrentSave);
    }

    public void Save(SaveData saveRoot)
    {
        if (saveRoot == null)
            return;

        enabledSave = false;
        saveRoot.SaveVersion++;
        saveRoot.LastSavedUtc = DateTime.UtcNow.ToString();

        saveRoot.SystemNpcSimulation = _systemNpcSimulationSaveService.Capture();
        saveRoot.SystemEncounter = _systemEncounterSaveService.Capture();
        saveRoot = DictionaryToList(saveRoot);

        string json = JsonUtility.ToJson(saveRoot, true);
        File.WriteAllText(GetFullPath(), json);

        eventBus.Publish(new GameSavedEvent());
        autosaveTimer = 0f;

        Debug.Log("Game saved to: " + GetFullPath());

        enabledSave = true;
    }

    private SaveData DictionaryToList(SaveData saveRoot)
    {
        saveRoot.MissionBlock.OffersByPlanet_List = new ();
        foreach (var pair in saveRoot.MissionBlock.OffersByPlanet)
        {
            pair.Value.PlanetId = pair.Key;
            saveRoot.MissionBlock.OffersByPlanet_List.Add(pair.Value);
        }

        return saveRoot;
    }

    public void Tick(float deltaTime)
    {
        // if (!hasUnsavedChanges)
        //     return;

        autosaveTimer += deltaTime;

        if (autosaveTimer >= _autosaveIntervalSeconds && enabledSave)
        {
            Save();
            Debug.Log("SaveService: Periodic autosave completed.");
        }
    }    

    public SaveData Load()
    {
        string path = GetFullPath();

        if (!File.Exists(path))
        {
            Debug.LogWarning("Save file not found.");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveData save = JsonUtility.FromJson<SaveData>(json);

            save = DictionaryFromList(save);
            _systemEncounterSaveService.Restore(save.SystemEncounter);
            _systemNpcSimulationSaveService.Restore(save.SystemNpcSimulation);

            return save;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load save: " + e.Message);
            return null;
        }
    }

    private SaveData DictionaryFromList(SaveData saveRoot)
    {
        saveRoot.MissionBlock.OffersByPlanet = new ();
        foreach (PlanetOfferedMissionData item in saveRoot.MissionBlock.OffersByPlanet_List)
            saveRoot.MissionBlock.OffersByPlanet.Add(item.PlanetId, item);

        return saveRoot;
    }

    public void DeleteSave()
    {
        string path = GetFullPath();

        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Save file deleted: " + path);
        }
    }
}