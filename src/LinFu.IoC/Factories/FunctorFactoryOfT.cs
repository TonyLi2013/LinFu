﻿using System;
using LinFu.IoC.Interfaces;

namespace LinFu.IoC.Factories
{
    /// <summary>
    ///     A class that converts a delegate into an <see cref="IFactory" /> instance.
    /// </summary>
    public class FunctorFactory<T> : IFactory<T>, IFactory
    {
        private readonly Func<IFactoryRequest, T> _factoryMethod;

        /// <summary>
        ///     Initializes the class with the given <paramref name="factoryMethod" />.
        /// </summary>
        /// <param name="factoryMethod">The delegate that will be used to instantiate a type.</param>
        public FunctorFactory(Func<IFactoryRequest, object> factoryMethod)
        {
            _factoryMethod = request => (T) factoryMethod(request);
        }

        /// <summary>
        ///     Initializes the class with the given <paramref name="factoryMethod" />.
        /// </summary>
        /// <param name="factoryMethod">The delegate that will be used to instantiate a type.</param>
        public FunctorFactory(Func<IFactoryRequest, T> factoryMethod)
        {
            _factoryMethod = factoryMethod;
        }


        /// <summary>
        ///     Instantiates an object reference using the given factory method.
        /// </summary>
        /// <param name="request">The <see cref="IFactoryRequest" /> instance that describes the requested service.</param>
        /// <returns>A non-null object reference that represents the service type.</returns>
        public object CreateInstance(IFactoryRequest request)
        {
            return _factoryMethod(request);
        }


        T IFactory<T>.CreateInstance(IFactoryRequest request)
        {
            return _factoryMethod(request);
        }
    }
}