using System;
using LinFu.AOP.Cecil.Interfaces;
using LinFu.AOP.Interfaces;
using Mono.Cecil;

namespace LinFu.AOP.Cecil.Extensions
{
    /// <summary>
    ///     Represents an extension class that adds method body interception support to the Mono.Cecil object model.
    /// </summary>
    public static class MethodBodyInterceptionExtensions
    {        
        /// <summary>
        ///     Intercepts all method bodies on the target item.
        /// </summary>
        /// <param name="target">The target to be modified.</param>
        public static void InterceptAllMethodBodies(this TypeDefinition target)
        {
            target.InterceptMethodBody(m => true);
        }       

        /// <summary>
        ///     Intercepts all method bodies on the target item.
        /// </summary>
        /// <param name="target">The target to be modified.</param>
        public static void InterceptAllMethodBodies(this AssemblyDefinition target)
        {
            target.InterceptMethodBody(m => true);
        }

        /// <summary>
        ///     Intercepts all method bodies on the target assembly. 
        /// </summary>
        /// <param name="target">The target assembly that will be modified.</param>
        /// <param name="methodFilter">The method filter that will be used to determine which methods will be modified.</param>
        public static void InterceptMethodBody(this AssemblyDefinition target,
            IMethodFilter methodFilter)
        {
            target.InterceptMethodBody(methodFilter.ShouldWeave);   
        }

        /// <summary>
        ///     Intercepts all method bodies on the target item.
        /// </summary>
        /// <param name="target">The target to be modified.</param>
        /// <param name="methodFilter">The method filter that will determine the methods that will be modified.</param>
        public static void InterceptMethodBody(this TypeDefinition target, IMethodFilter methodFilter)
        {
            target.InterceptMethodBody(methodFilter.ShouldWeave);
        }

        /// <summary>
        ///     Intercepts all method bodies on the target item.
        /// </summary>
        /// <param name="target">The target to be modified.</param>
        /// <param name="methodFilter">The method filter that will determine the methods that will be modified.</param>
        public static void InterceptMethodBody(this AssemblyDefinition target,
            Func<MethodReference, bool> methodFilter)
        {
            var typeFilter = GetTypeFilter();
            target.Accept(new ImplementModifiableType(typeFilter));

            IMethodWeaver interceptMethodBody = new InterceptMethodBody(methodFilter);
            target.WeaveWith(interceptMethodBody);
        }
                
        /// <summary>
        ///     Intercepts all method bodies on the target item.
        /// </summary>
        /// <param name="target">The target to be modified.</param>
        /// <param name="methodFilter">The method filter that will determine the methods that will be modified.</param>
        public static void InterceptMethodBody(this TypeDefinition target,
            Func<MethodReference, bool> methodFilter)
        {
            var typeFilter = GetTypeFilter();
            target.Accept(new ImplementModifiableType(typeFilter));

            IMethodWeaver interceptMethodBody = new InterceptMethodBody(methodFilter);
            target.WeaveWith(interceptMethodBody);
        }

        private static Func<TypeReference, bool> GetTypeFilter()
        {
            return type =>
            {
                var actualType = type.Resolve();
                if (actualType.IsValueType || actualType.IsInterface)
                    return false;

                return actualType.IsClass;
            };
        }
    }
}