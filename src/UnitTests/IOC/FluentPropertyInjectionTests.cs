﻿using LinFu.IoC;
using Moq;
using NUnit.Framework;
using SampleLibrary;

namespace LinFu.UnitTests.IOC
{
    [TestFixture]
    public class FluentPropertyInjectionTests
    {
        private static void TestPropertyInjection(string serviceName)
        {
            var mockTarget = new Mock<IInjectionTarget>();
            mockTarget.Expect(t => t.SetValue(123));
            var target = mockTarget.Object;

            var container = new ServiceContainer();
            container.AddService(serviceName, target);

            // Use the named fluent interface for
            // named instances
            if (!string.IsNullOrEmpty(serviceName))
                container.Initialize<IInjectionTarget>(serviceName)
                    .With(service => service.SetValue(123));
            else
                container.Initialize<IInjectionTarget>()
                    .With(service => service.SetValue(123));
            var result = container.GetService<IInjectionTarget>(serviceName);
            Assert.IsNotNull(result);

            // The container should initialize the
            // service on every request
            mockTarget.VerifyAll();
        }

        [Test]
        public void NamedPropertyMustBeInjectedIntoInjectionTarget()
        {
            var serviceName = "MyService";

            TestPropertyInjection(serviceName);
        }

        [Test]
        public void UnnamedPropertyMustBeInjectedIntoInjectionTarget()
        {
            string serviceName = null;

            TestPropertyInjection(serviceName);
        }
    }
}