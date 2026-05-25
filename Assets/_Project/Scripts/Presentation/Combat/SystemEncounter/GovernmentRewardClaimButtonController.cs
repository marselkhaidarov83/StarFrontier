using TMPro;
using UnityEngine;
using UnityEngine.UI;
public sealed class GovernmentRewardClaimButtonController : CustomMonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private GameObject missionBlock;
    [SerializeField] private GameObject missionBlock2;

    [Header("Planet Context")]
    [SerializeField] private string currentSystemId = "system_debug";
    [SerializeField] private bool isCurrentPlanetInhabited = true;

    private IGovernmentRewardService _governmentRewardService;

    private void Awake()
    {
        _governmentRewardService = Bootstrapper.Instance.ServiceRegistry.Get<IGovernmentRewardService>();

        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimClicked);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(OnClaimClicked);
    }

    public void SetPlanetContext(string systemId, bool isInhabited)
    {
        currentSystemId = systemId;
        isCurrentPlanetInhabited = isInhabited;

        Refresh();
    }

    public void Refresh()
    {
        bool canClaim = _governmentRewardService != null &&
                        _governmentRewardService.CanClaimReward(
                            currentSystemId,
                            isCurrentPlanetInhabited
                        );

        if (IsDebug())
        {
            Debug.Log("[GovernmentRewardClaimButtonController] _governmentRewardService = " + _governmentRewardService);
            Debug.Log("[GovernmentRewardClaimButtonController] currentSystemId = " + currentSystemId);
            Debug.Log("[GovernmentRewardClaimButtonController] isCurrentPlanetInhabited = " + isCurrentPlanetInhabited);
            Debug.Log("[GovernmentRewardClaimButtonController] canClaim = " + canClaim);
        }

        if (root != null)
            root.SetActive(canClaim);

        if (missionBlock != null)
            missionBlock.SetActive(!canClaim);
        if (missionBlock2 != null)
            missionBlock2.SetActive(!canClaim);

        if (claimButton != null)
            claimButton.interactable = canClaim;

        if (titleText != null)
            titleText.text = "Government Reward";

        if (descriptionText != null)
        {
            descriptionText.text = canClaim
                ? "System secured. Claim your reward from the local government."
                : "No government reward available.";
        }
    }

    private void OnClaimClicked()
    {
        if (_governmentRewardService == null)
            return;

        GovernmentRewardResult result = _governmentRewardService.ClaimReward(
            currentSystemId,
            isCurrentPlanetInhabited
        );

        Debug.Log(
            $"[GovernmentRewardClaimButton] Claim result: {result.Success}, Credits: {result.Credits}, XP: {result.Xp}, Reason: {result.Reason}"
        );

        Refresh();
    }
}