using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ArrivalFxViewEditModeTests
{
    private GameObject _map, _flash;
    private ArrivalFxView2 _view;
    private SpriteRenderer _image;
    [SetUp] public void Setup()
    {
        _map = new GameObject("SystemMap");
        _flash = new GameObject("ArrivalFlash", typeof(SpriteRenderer));
        _flash.transform.SetParent(_map.transform);
        _view = _flash.AddComponent<ArrivalFxView2>();
        _image = _flash.GetComponent<SpriteRenderer>();
        Field("image").SetValue(_view, _image);
    }
    private static FieldInfo Field(string name) => typeof(ArrivalFxView2).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic);
    [TearDown] public void Cleanup() { Object.DestroyImmediate(_map); }

    [Test] public void Play_WhenParentHidden_DoesNotStartCoroutineOrReopenMap()
    {
        _map.SetActive(false); _flash.SetActive(false);
        Assert.DoesNotThrow(() => _view.Play(new Vector3(12, 34, -2)));
        Assert.That(_map.activeSelf, Is.False);
        Assert.That(_flash.activeSelf, Is.False);
        Assert.That(Field("_animationCoroutine").GetValue(_view), Is.Null);
        Assert.That(_flash.transform.position, Is.EqualTo(new Vector3(12, 34, -2)));
    }
    [Test] public void Play_WhenComponentDisabled_DoesNotStartCoroutine()
    {
        _view.enabled = false;
        Assert.DoesNotThrow(() => _view.Play(Vector3.one));
        Assert.That(_view.enabled, Is.False);
        Assert.That(_flash.activeSelf, Is.False);
        Assert.That(Field("_animationCoroutine").GetValue(_view), Is.Null);
    }
    [Test] public void Disable_ClearsVisualAndDoesNotLeaveFlashForMapReturn()
    {
        _image.color = new Color(.2f, .4f, .6f, .25f);
        _flash.transform.localScale = Vector3.one * 8;
        _map.SetActive(false);
        // EditMode lifecycle is invoked directly; do not use Unity SendMessage.
        typeof(ArrivalFxView2).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_view, null);
        Assert.That(_flash.activeSelf, Is.False);
        Assert.That(_flash.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(_image.color, Is.EqualTo(new Color(.2f, .4f, .6f, 1f)));
        Assert.That(Field("_animationCoroutine").GetValue(_view), Is.Null);
        _map.SetActive(true);
        Assert.That(_flash.activeInHierarchy, Is.False);
    }
    [Test] public void SkippedEffect_CanReceiveNextRequestAfterMapReturns()
    {
        _map.SetActive(false); _view.Play(Vector3.one);
        _map.SetActive(true);
        // Zero-duration playback finishes synchronously, without an Editor coroutine.
        Field("duration").SetValue(_view, 0f);
        Assert.DoesNotThrow(() => _view.Play(new Vector3(4, 5, 6)));
        Assert.That(_flash.transform.position, Is.EqualTo(new Vector3(4, 5, 6)));
        Assert.That(_flash.activeSelf, Is.False);
        Assert.That(_flash.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(Field("_animationCoroutine").GetValue(_view), Is.Null);
    }
}
