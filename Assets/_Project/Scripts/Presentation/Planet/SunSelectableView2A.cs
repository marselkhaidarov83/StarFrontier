using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SunSelectableView2A :
    CustomMonoBehaviour,
    IPointerClickHandler
{
    [SerializeField]
    private SunNodeView sunNodeView;

    public void Initialize(
        SunNodeView nodeView)
    {
        sunNodeView = nodeView;

        if (sunNodeView == null)
            sunNodeView =
                GetComponentInParent<SunNodeView>();
    }

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (sunNodeView == null)
            sunNodeView =
                GetComponentInParent<SunNodeView>();

        if (sunNodeView == null)
        {
            Debug.LogWarning(
                "[SunSelectableView2A] SunNodeView is null.",
                this);

            return;
        }

        sunNodeView.OpenSystemObjectsPanelFromSun();
    }
}
