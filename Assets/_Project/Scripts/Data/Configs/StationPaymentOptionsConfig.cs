using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StationPaymentOptionsConfig", menuName = "StarFrontier/Configs/Station/Payment Options")]
public sealed class StationPaymentOptionsConfig : BaseConfig
{
    [Tooltip("Варианты оплаты одной операции. У каждого варианта свои ресурсы и длительность.")]
    public StationPaymentOption[] Options = Array.Empty<StationPaymentOption>();
}
