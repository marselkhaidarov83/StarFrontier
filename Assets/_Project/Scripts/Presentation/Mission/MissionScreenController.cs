using System.Collections.Generic;
using UnityEngine;

public class MissionScreenController : CustomMonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform missionListContent;
    [SerializeField] private MissionCardView missionCardPrefab;

    [Header("Icons")]
    [SerializeField] private Sprite deliveryIcon;
    [SerializeField] private Sprite eliminationIcon;
    [SerializeField] private Sprite reconIcon;

    private IMissionService _missionService;
    private SimpleEventBus simpleEventBus;

    private readonly List<MissionCardView> _spawnedCards = new();
    private MissionInstanceData _selectedMission;

    public void Initialize()
    {
        simpleEventBus = Bootstrapper.Instance.ServiceRegistry.Get<SimpleEventBus>();
        _missionService = Bootstrapper.Instance.ServiceRegistry.Get<IMissionService>();

        SubscribeToEvents();

        LogCustom("initialized");
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Subscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
        simpleEventBus.Subscribe<MissionAcceptedEvent>(OnMissionAccepted);
    }

    private void UnsubscribeFromEvents()
    {
        if (simpleEventBus == null)
            return;

        simpleEventBus.Unsubscribe<GovernmentEnteredEvent>(OnGovernmentEntered);
        simpleEventBus.Unsubscribe<MissionAcceptedEvent>(OnMissionAccepted);
    }

    private void OnGovernmentEntered(GovernmentEnteredEvent evt)
    {
        RefreshMissionBoard();
    }

    private void OnMissionAccepted(MissionAcceptedEvent evt)
    {
        RefreshMissionBoard();
    }

    public void RefreshMissionBoard()
    {
        ClearCards();

        List<MissionInstanceData> allMissions = new();
        allMissions.AddRange(_missionService.GetAvailableMissions());
        allMissions.AddRange(_missionService.GetActiveMissions());
        allMissions.AddRange(_missionService.GetCompletedMissions());

        foreach (MissionInstanceData mission in allMissions)
        {
            MissionCardView card = Instantiate(missionCardPrefab, missionListContent);
            Sprite icon = GetMissionIcon(mission.MissionType);
            bool isSelected = _selectedMission != null &&
                            _selectedMission.MissionRuntimeId == mission.MissionRuntimeId;

            card.Bind(mission, icon, HandleMissionSelected, HandleMissionDone, isSelected);
            _spawnedCards.Add(card);
        }

        // if (_selectedMission != null)
        // {
        //     MissionInstanceData refreshedSelection = _missionService.GetMissionById(_selectedMission.MissionRuntimeId);

        //     if (refreshedSelection != null)
        //     {
        //         _selectedMission = refreshedSelection;
        //         detailPanel.Bind(_selectedMission, GetMissionIcon(_selectedMission.MissionType), HandleAcceptMission);
        //         return;
        //     }
        // }

        // if (allMissions.Count > 0)
        //     HandleMissionSelected(allMissions[0]);
        // else
        //     _selectedMission = null;
    }

    private void HandleMissionSelected(MissionInstanceData mission)
    {
        _selectedMission = mission;
        Sprite icon = GetMissionIcon(mission.MissionType);
        // detailPanel.Bind(mission, icon, HandleAcceptMission);

        RefreshMissionBoardSelectionOnly();
    }

    private void HandleMissionDone(MissionInstanceData mission)
    {
        _missionService.CompleteMission(mission.MissionRuntimeId);
        simpleEventBus.Publish(new GovernmentEnteredEvent());
    }

    private void RefreshMissionBoardSelectionOnly()
    {
        foreach (MissionCardView card in _spawnedCards)
        {
            if (card == null)
            {
                continue;
            }
        }

        RefreshMissionBoard();
    }

    private void HandleAcceptMission(MissionInstanceData mission)
    {
        if (mission == null)
        {
            return;
        }

        bool accepted = _missionService.AcceptMission(mission.MissionRuntimeId);

        if (!accepted)
        {
            Debug.LogWarning($"MissionBoard: Failed to accept mission {mission.MissionRuntimeId}");
            return;
        }

        _selectedMission = _missionService.GetMissionById(mission.MissionRuntimeId);
        RefreshMissionBoard();
    }

    private Sprite GetMissionIcon(MissionType missionType)
    {
        return missionType switch
        {
            MissionType.Delivery => deliveryIcon,
            MissionType.Elimination => eliminationIcon,
            MissionType.Recon => reconIcon,
            _ => null
        };
    }

    private void ClearCards()
    {
        foreach (MissionCardView card in _spawnedCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }

        _spawnedCards.Clear();
    }
}