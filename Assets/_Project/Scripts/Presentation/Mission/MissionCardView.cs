using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

    public class MissionCardView : MonoBehaviour
    {
        [Header("Base")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image missionTypeIcon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text shortDescriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text stateBadgeText;
        [SerializeField] private GameObject selectedFrame;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button doneButton;

        private MissionInstanceData _missionData;
        private Action<MissionInstanceData> _onSelected;
        private Action<MissionInstanceData> _onDone;

        public void Bind(
            MissionInstanceData missionData,
            Sprite missionIcon,
            Action<MissionInstanceData> onSelected,
            Action<MissionInstanceData> onDone,
            bool isSelected)
        {
            _missionData = missionData;
            _onSelected = onSelected;
            _onDone = onDone;

            if (missionTypeIcon != null)
            {
                missionTypeIcon.sprite = missionIcon;
                missionTypeIcon.enabled = missionIcon != null;
            }

            if (titleText != null)
                titleText.text = missionData != null ? missionData.Title : "Unknown Mission";

            if (shortDescriptionText != null)
                shortDescriptionText.text = missionData != null ? missionData.Description : string.Empty;

            if (statusText != null)
                statusText.text = missionData != null ? $"Status: {missionData.Status}" : "Status: Unknown";

            if (rewardText != null)
            {
                if (missionData != null && missionData.Reward != null)
                    rewardText.text = $"Reward: {missionData.Reward.Credits} cr / {missionData.Reward.Xp} xp";
                else
                    rewardText.text = "Reward: -";
            }

            if (progressText != null)
                progressText.text = BuildProgressText(missionData);

            if (stateBadgeText != null)
                stateBadgeText.text = BuildBadgeText(missionData);

            if (selectedFrame != null)
                selectedFrame.SetActive(isSelected);

            ApplyVisualState(missionData);
            ApplyVisualStateButtonDone(missionData);

            if (doneButton != null)
            {
                doneButton.onClick.RemoveAllListeners();
                doneButton.onClick.AddListener(HandleDone);
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(HandleSelected);
            }
        }

        private void HandleSelected()
        {
            _onSelected?.Invoke(_missionData);
        }

        private void HandleDone()
        {
            _onDone?.Invoke(_missionData);
        }

        private string BuildProgressText(MissionInstanceData mission)
        {
            if (mission == null || mission.Objective == null)
            {
                return "Progress: -";
            }

            return mission.MissionType switch
            {
                MissionType.Delivery => $"Progress: {(mission.IsReadyToTurnIn ? "Ready to turn in" : "In progress")}",
                MissionType.Elimination => $"Progress: {mission.Objective.CurrentAmount}/{mission.Objective.RequiredAmount}",
                MissionType.Recon => $"Progress: {mission.Objective.CurrentAmount}/{mission.Objective.RequiredAmount}",
                _ => "Progress: -"
            };
        }

        private string BuildBadgeText(MissionInstanceData mission)
        {
            if (mission == null)
            {
                return "UNKNOWN";
            }

            return mission.Status switch
            {
                MissionStatus.Available => "AVAILABLE",
                MissionStatus.Accepted => "ACCEPTED",
                MissionStatus.ReadyToComplete => "READY",
                MissionStatus.Completed => "COMPLETED",
                MissionStatus.Failed => "FAILED",
                MissionStatus.Expired => "EXPIRED",
                _ => "UNKNOWN"
            };
        }

    private void ApplyVisualStateButtonDone(MissionInstanceData mission)
    {
        IGameSessionService gameSessionService = Bootstrapper.Instance.ServiceRegistry.Get<IGameSessionService>();
        switch (mission.MissionType)
        {
            case MissionType.Delivery:
            case MissionType.Elimination:
                doneButton.gameObject.SetActive(
                    mission.TargetPlanetId == gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId &&
                    mission.Status == MissionStatus.ReadyToComplete);
                break;
            case MissionType.Recon:
                doneButton.gameObject.SetActive(
                    mission.TargetPlanetId == gameSessionService.CurrentSave.PlayerProfile.CurrentPlanetId &&
                    mission.TargetSystemId == gameSessionService.CurrentSave.PlayerProfile.CurrentSystemId &&
                    mission.Status == MissionStatus.ReadyToComplete);
                break;
        }
    }

        private void ApplyVisualState(MissionInstanceData mission)
        {
            if (backgroundImage == null || mission == null)
            {
                return;
            }

            switch (mission.Status)
            {
                case MissionStatus.Available:
                    backgroundImage.color = new Color(1f, 1f, 1f, 1f);
                    break;

                case MissionStatus.Accepted:
                    backgroundImage.color = new Color(0.9f, 0.95f, 1f, 1f);
                    break;

                case MissionStatus.ReadyToComplete:
                    backgroundImage.color = new Color(1f, 0.95f, 0.8f, 1f);
                    break;

                case MissionStatus.Completed:
                    backgroundImage.color = new Color(0.85f, 1f, 0.85f, 1f);
                    break;

                case MissionStatus.Failed:
                    backgroundImage.color = new Color(1f, 0.85f, 0.85f, 1f);
                    break;

                case MissionStatus.Expired:
                    backgroundImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    break;
            }
        }
    }