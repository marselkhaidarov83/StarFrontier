using UnityEngine;

// Конфиг PlanetMissionOfferEntryConfig содержит настройки соответствующей игровой системы и используется связанными сервисами и экранными представлениями.
[CreateAssetMenu(fileName = "PlanetMissionOfferEntryConfig", menuName = "StarFrontier/Configs/Mission/PlanetMissionOfferEntry")]
public class PlanetMissionOfferEntryConfig : BaseConfig
{
    [Tooltip("Шаблон миссии, из которого будет создано конкретное задание. Используется генератором планетарных предложений.")]
    [SerializeField] private MissionTemplateConfig missionTemplateConfig;
        // public string MissionTemplateId;
    [Tooltip("Относительный вес выбора этой записи. Чем больше значение, тем чаще запись выбирается среди других подходящих.")]
    [SerializeField] private int weight = 1;
    [Tooltip("Включает или выключает эту запись без удаления из списка. Выключенные записи генератор пропускает.")]
    [SerializeField] private bool isEnabled = true;

    public MissionTemplateConfig MissionTemplateConfig => missionTemplateConfig;
    public int Weight => weight;
    public bool IsEnabled => isEnabled;
}
