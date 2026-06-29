using System;
using NUnit.Framework;

public sealed class ServiceRegistryEditModeTests
{
    [Test]
    public void RegisterReadyInstance_GetReturnsSameInstance()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        TestService original =
            new TestService();

        registry.Register<TestService>(original);

        TestService restored =
            registry.Get<TestService>();

        Assert.AreSame(original, restored);
    }

    [Test]
    public void RegisterByInterface_GetReturnsSameInstance()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        TestService original =
            new TestService();

        registry.Register<ITestService>(original);

        ITestService restored =
            registry.Get<ITestService>();

        Assert.AreSame(original, restored);
    }

    [Test]
    public void RegisterByInterface_DoesNotRegisterConcreteType()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        TestService original =
            new TestService();

        registry.Register<ITestService>(original);

        bool foundConcrete =
            registry.TryGet<TestService>(
                out TestService concreteService);

        Assert.IsFalse(foundConcrete);
        Assert.IsNull(concreteService);
    }

    [Test]
    public void RegisterNull_ThrowsArgumentNullException()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        ITestService nullService = null;

        Assert.Throws<ArgumentNullException>(
            () => registry.Register<ITestService>(
                nullService));
    }

    [Test]
    public void RegisterSameServiceTypeTwice_Throws()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        TestService first =
            new TestService();

        TestService second =
            new TestService();

        registry.Register<ITestService>(first);

        Assert.Throws<InvalidOperationException>(
            () => registry.Register<ITestService>(
                second));
    }

    [Test]
    public void TryGetRegistered_ReturnsTrueAndSameInstance()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        TestService original =
            new TestService();

        registry.Register<ITestService>(original);

        bool found =
            registry.TryGet<ITestService>(
                out ITestService restored);

        Assert.IsTrue(found);
        Assert.AreSame(original, restored);
    }

    [Test]
    public void TryGetMissing_ReturnsFalse()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        bool found =
            registry.TryGet<ITestService>(
                out ITestService service);

        Assert.IsFalse(found);
        Assert.IsNull(service);
    }

    [Test]
    public void GetMissing_ThrowsInvalidOperationException()
    {
        ServiceRegistry registry =
            new ServiceRegistry();

        Assert.Throws<InvalidOperationException>(
            () => registry.Get<ITestService>());
    }

    private interface ITestService
    {
    }

    private sealed class TestService :
        ITestService
    {
    }
}