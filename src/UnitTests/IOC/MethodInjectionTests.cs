﻿using System;
using LinFu.IoC;
using Xunit;
using SampleLibrary;
using SampleLibrary.IOC;

namespace LinFu.UnitTests.IOC
{
    public class MethodInjectionTests : BaseTestFixture
    {
        [Fact]
        public void ShouldAutoInjectMethod()
        {
            var container = new ServiceContainer();
            container.LoadFrom(AppDomain.CurrentDomain.BaseDirectory, "LinFu*.dll");

            var instance = new SampleClassWithInjectionMethod();

            // Initialize the container
            container.Inject<ISampleService>().Using<SampleClass>().OncePerRequest();
            container.Inject<ISampleService>("MyService").Using(c => instance).OncePerRequest();

            var result = container.GetService<ISampleService>("MyService");
            Assert.Same(result, instance);

            // On initialization, the instance.Property value
            // should be a SampleClass type
            Assert.NotNull(instance.Property);
            Assert.Equal(typeof(SampleClass), instance.Property?.GetType());
        }
    }
}