using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MarketProfile", menuName = "StarFrontier/Configs/MarketProfile")]
public class MarketProfileConfig : BaseConfig
{
    [SerializeField] private MarketSpecializationType specialization;
    [SerializeField] private List<MarketItemEntry> items = new List<MarketItemEntry>();

    public MarketSpecializationType Specialization => specialization;
    public List<MarketItemEntry> Items => items;
}