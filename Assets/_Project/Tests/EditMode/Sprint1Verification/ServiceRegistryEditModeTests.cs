using System;
using NUnit.Framework;

namespace StarFrontier.Tests.Sprint1
{
    [TestFixture]
    public sealed class ServiceRegistryEditModeTests
    {
        private ServiceRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new ServiceRegistry();
        }

        [Test]
        public void RegisterConcrete_Get_ReturnsTheSameInstance()
        {
            var service = new ConcreteService();

            _registry.Register(service);

            Assert.That(_registry.Get<ConcreteService>(), Is.SameAs(service));
        }

        [Test]
        public void RegisterInterface_Get_ReturnsTheSameInstance()
        {
            IServiceContract service = new ConcreteService();

            _registry.Register<IServiceContract>(service);

            Assert.That(_registry.Get<IServiceContract>(), Is.SameAs(service));
        }

        [Test]
        public void Register_DuplicateType_ThrowsAndKeepsOriginalInstance()
        {
            var original = new ConcreteService();
            _registry.Register<IServiceContract>(original);

            Assert.Throws<InvalidOperationException>(
                () => _registry.Register<IServiceContract>(new ConcreteService()));
            Assert.That(_registry.Get<IServiceContract>(), Is.SameAs(original),
                "A failed duplicate registration must not partially change the registry.");
        }

        [Test]
        public void Register_Null_ThrowsAndDoesNotRegisterType()
        {
            Assert.Throws<ArgumentNullException>(
                () => _registry.Register<IServiceContract>(null),
                "Sprint 1 DoD requires explicit null protection.");

            Assert.That(_registry.TryGet<IServiceContract>(out _), Is.False,
                "A rejected null registration must leave no registry entry behind.");
        }

        [Test]
        public void Get_MissingService_ThrowsWithServiceTypeInMessage()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _registry.Get<IServiceContract>());

            StringAssert.Contains(nameof(IServiceContract), exception.Message);
        }

        [Test]
        public void TryGet_MissingService_ReturnsFalseAndDefault()
        {
            bool found = _registry.TryGet<IServiceContract>(out IServiceContract service);

            Assert.That(found, Is.False);
            Assert.That(service, Is.Null);
        }

        [Test]
        public void RegistrationByInterface_DoesNotCreateAmbiguousConcreteAlias()
        {
            IServiceContract service = new ConcreteService();
            _registry.Register<IServiceContract>(service);

            Assert.That(_registry.TryGet(out ConcreteService concrete), Is.False,
                "Registration is exact-type based; aliases must be explicit and documented.");
            Assert.That(concrete, Is.Null);
        }

        private interface IServiceContract
        {
        }

        private sealed class ConcreteService : IServiceContract
        {
        }
    }
}
