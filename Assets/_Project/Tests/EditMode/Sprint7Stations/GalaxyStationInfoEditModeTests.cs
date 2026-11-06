using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public sealed class GalaxyStationInfoEditModeTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private GameObject _host, _label;
    private GalaxySystemInfoPanel2A _panel;
    private TMP_Text _text;
    private StationConfig _station;
    private StarSystemConfig _system;
    private StationTypeConfig _type;
    private static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(target, value); return; }
        }
        Assert.Fail(name);
    }
    private T Asset<T>() where T : ScriptableObject
    { var a = ScriptableObject.CreateInstance<T>(); _assets.Add(a); return a; }
    [SetUp] public void Setup()
    {
        _host = new GameObject("GalaxyStationInfo"); _panel = _host.AddComponent<GalaxySystemInfoPanel2A>();
        _label = new GameObject("StationLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        _text = _label.GetComponent<TMP_Text>(); Set(_panel, "baseText", _text);
        _station = Asset<StationConfig>(); Set(_station, "id", "station"); Set(_station, "displayName", "Золотая пристань");
        Set(_station, "isActive", true);
        _type = Asset<StationTypeConfig>(); Set(_type, "stationType", StationType.Trade); Set(_station, "stationTypeConfig", _type);
        _system = Asset<StarSystemConfig>(); Set(_system, "station", _station);
    }
    private void Display(StarSystemConfig config, StationRuntimeState station = null)
    {
        typeof(GalaxySystemInfoPanel2A).GetMethod("SetBaseText", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_panel, new object[] { config, station == null ? null : new StarSystemRuntimeState { Station = station } });
    }
    [TearDown] public void Cleanup()
    {
        Object.DestroyImmediate(_host); Object.DestroyImmediate(_label);
        foreach (var a in _assets) Object.DestroyImmediate(a); _assets.Clear();
    }
    [TestCase(StationType.Military, "Военная база")]
    [TestCase(StationType.RangerBase, "База рейнджеров")]
    [TestCase(StationType.Trade, "Торговая станция")]
    [TestCase(StationType.Science, "Научная станция")]
    [TestCase(StationType.Medical, "Медицинская станция")]
    public void StationLine_UsesTypeOnly(StationType type, string title)
    {
        Set(_type, "stationType", type); Display(_system, new StationRuntimeState { Level = 1 });
        Assert.That(_text.text, Is.EqualTo("Станция: " + title));
        Assert.That(_text.color, Is.EqualTo(Color.white));
    }
    [Test] public void UnbuiltAndFirstConstruction_AreGray_UntilConstructionCompletes()
    {
        var runtime = new StationRuntimeState { Level = 0 };
        Display(_system, runtime); Assert.That(_text.color, Is.EqualTo(Color.gray));
        runtime.Development = new StationOperationRuntimeState { Kind = StationOperationKind.Construction, TargetLevel = 1 };
        Display(_system, runtime); Assert.That(_text.color, Is.EqualTo(Color.gray));
        runtime.Level = 1; runtime.Development = null;
        Display(_system, runtime); Assert.That(_text.color, Is.EqualTo(Color.white));
    }
    [TestCase(1)] [TestCase(10)] public void BuiltLevel_IsWhite_AndUsesRuntimeOverInitialConfig(int level)
    {
        Set(_station, "initialLevel", 0); Display(_system, new StationRuntimeState { Level = level });
        Assert.That(_text.color, Is.EqualTo(Color.white));
    }
    [Test] public void SwitchingToUnbuilt_ResetsPreviousWhiteColor()
    {
        Display(_system, new StationRuntimeState { Level = 3 });
        Display(_system, new StationRuntimeState { Level = 0 });
        Assert.That(_text.color, Is.EqualTo(Color.gray));
    }
    [Test] public void MissingRuntime_UsesInitialLevel_AndClearingResetsLabel()
    {
        Set(_station, "initialLevel", 1); Display(_system); Assert.That(_text.color, Is.EqualTo(Color.white));
        Set(_station, "initialLevel", 0); Display(_system); Assert.That(_text.color, Is.EqualTo(Color.gray));
        Display(null); Assert.That(_text.text, Is.EqualTo("Станция:")); Assert.That(_text.color, Is.EqualTo(Color.white));
    }
    [Test] public void MissingStation_DoesNotInventPlanetConstructionAvailability()
    {
        Set(_system, "station", null); Display(_system);
        Assert.That(_text.text, Is.EqualTo("Станция: нет"));
    }
}
