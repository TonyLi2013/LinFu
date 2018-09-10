﻿using System;
using System.Collections.Generic;
using LinFu.AOP.Cecil.Interfaces;
using LinFu.AOP.Interfaces;
using LinFu.Reflection.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LinFu.AOP.Cecil
{
    /// <summary>
    ///     Represents a method rewriter that modifies a method body to support dynamic exception handling.
    /// </summary>
    public class CatchAllThrownExceptions : BaseMethodRewriter, IMethodWeaver
    {
        private readonly Func<MethodReference, bool> _methodFilter;
        private VariableDefinition _exception;
        private VariableDefinition _exceptionHandler;
        private VariableDefinition _exceptionInfo;
        private VariableDefinition _invocationInfo;
        private VariableDefinition _returnValue;
        private TypeReference _voidType;

        /// <summary>
        /// Creates a new instance of the <see cref="CatchAllThrownExceptions"/> class...
        /// </summary>
        public CatchAllThrownExceptions()
        {
            // Rewrite everything by default
            _methodFilter = _ => true;
        }

        /// <summary>
        /// Creates a new instance of the type that supports selecting method rewriting.
        /// </summary>
        /// <param name="methodFilter">The filter that determines which methods will be rewritten.</param>
        public CatchAllThrownExceptions(Func<MethodReference, bool> methodFilter)
        {
            _methodFilter = methodFilter;
        }
        
        /// <summary>
        ///     Adds additional references to the target module.
        /// </summary>
        /// <param name="module">The host module.</param>
        public override void ImportReferences(ModuleDefinition module)
        {
            _voidType = module.Import(typeof(void));
        }

        /// <summary>
        ///     Adds local variables to the <paramref name="hostMethod" />.
        /// </summary>
        /// <param name="hostMethod">The target method.</param>
        public override void AddLocals(MethodDefinition hostMethod)
        {
            _exception = hostMethod.AddLocal<Exception>();
            _invocationInfo = hostMethod.AddLocal<IInvocationInfo>();
            _exceptionHandler = hostMethod.AddLocal<IExceptionHandler>();
            _exceptionInfo = hostMethod.AddLocal<IExceptionHandlerInfo>();

            var returnType = hostMethod.ReturnType;
            if (returnType != _voidType)
                _returnValue = hostMethod.AddLocal<object>();
        }

        /// <summary>
        ///     Determines whether or not the given method should be modified.
        /// </summary>
        /// <param name="targetMethod">The target method.</param>
        /// <returns>A <see cref="bool" /> indicating whether or not a method should be rewritten.</returns>
        protected override bool ShouldRewrite(MethodDefinition targetMethod)
        {
            return _methodFilter(targetMethod);
        }

        /// <summary>
        ///     Rewrites the instructions in the target method body to support dynamic exception handling.
        /// </summary>
        /// <param name="targetMethod">The target method.</param>
        /// <param name="IL">The <see cref="ILProcessor" /> instance that represents the method body.</param>
        /// <param name="oldInstructions">The IL instructions of the original method body.</param>
        protected override void RewriteMethodBody(MethodDefinition targetMethod, ILProcessor IL,
            IEnumerable<Instruction> oldInstructions)
        {
            var endOfOriginalInstructionBlock = IL.Create(OpCodes.Nop);
            var addOriginalInstructions = new AddOriginalInstructions(oldInstructions, endOfOriginalInstructionBlock);


            var endLabel = IL.Create(OpCodes.Nop);
            var tryStart = IL.Create(OpCodes.Nop);
            var tryEnd = IL.Create(OpCodes.Nop);
            var catchStart = IL.Create(OpCodes.Nop);
            var catchEnd = IL.Create(OpCodes.Nop);

            var module = IL.Body.Method.DeclaringType.Module;
            var handler = new ExceptionHandler(ExceptionHandlerType.Catch);
            var body = targetMethod.Body;
            body.ExceptionHandlers.Add(handler);

            handler.CatchType = module.ImportType<Exception>();

            handler.TryStart = tryStart;
            handler.TryEnd = tryEnd;

            handler.HandlerStart = catchStart;
            handler.HandlerEnd = catchEnd;

            var emitter = new InvocationInfoEmitter(true);

            var returnType = targetMethod.ReturnType;

            // try {
            IL.Append(tryStart);
            addOriginalInstructions.Emit(IL);

            IL.Append(endOfOriginalInstructionBlock);
            if (returnType != _voidType && _returnValue != null) IL.Emit(OpCodes.Stloc, _returnValue);

            IL.Emit(OpCodes.Leave, endLabel);

            // }            
            IL.Append(tryEnd);
            // catch (Exception ex) {
            IL.Append(catchStart);
            IL.Emit(OpCodes.Stloc, _exception);

            SaveExceptionInfo(targetMethod, emitter);
            IL.Emit(OpCodes.Ldloc, _exceptionInfo);

            var getHandlerMethodInfo = typeof(ExceptionHandlerRegistry).GetMethod("GetHandler");
            var getHandlerMethod = module.Import(getHandlerMethodInfo);
            IL.Emit(OpCodes.Call, getHandlerMethod);
            IL.Emit(OpCodes.Stloc, _exceptionHandler);

            // if (exceptionHandler == null) 
            //      throw;
            var doRethrow = IL.Create(OpCodes.Nop);
            IL.Emit(OpCodes.Ldloc, _exceptionHandler);
            IL.Emit(OpCodes.Brfalse, doRethrow);


            // if (handler.CanCatch(exceptionInfo)) {
            var leaveBlock = IL.Create(OpCodes.Nop);
            var canCatch = module.ImportMethod<IExceptionHandler>("CanCatch");
            IL.Emit(OpCodes.Ldloc, _exceptionHandler);
            IL.Emit(OpCodes.Ldloc, _exceptionInfo);
            IL.Emit(OpCodes.Callvirt, canCatch);
            IL.Emit(OpCodes.Brfalse, doRethrow);

            var catchMethod = module.ImportMethod<IExceptionHandler>("Catch");
            IL.Emit(OpCodes.Ldloc, _exceptionHandler);
            IL.Emit(OpCodes.Ldloc, _exceptionInfo);
            IL.Emit(OpCodes.Callvirt, catchMethod);
            // }


            var getShouldSkipRethrow = module.ImportMethod<IExceptionHandlerInfo>("get_ShouldSkipRethrow");
            IL.Emit(OpCodes.Ldloc, _exceptionInfo);
            IL.Emit(OpCodes.Callvirt, getShouldSkipRethrow);
            IL.Emit(OpCodes.Brfalse, doRethrow);

            IL.Emit(OpCodes.Br, leaveBlock);

            IL.Append(doRethrow);
            IL.Emit(OpCodes.Rethrow);

            IL.Append(leaveBlock);

            IL.Emit(OpCodes.Leave, endLabel);
            IL.Append(catchEnd);
            // }
            IL.Append(endLabel);

            if (returnType != _voidType && _returnValue != null)
            {
                var returnOriginalValue = IL.Create(OpCodes.Nop);
                var getReturnValue = module.ImportMethod<IExceptionHandlerInfo>("get_ReturnValue");

                IL.Emit(OpCodes.Ldloc, _exceptionInfo);
                IL.Emit(OpCodes.Brfalse, returnOriginalValue);

                IL.Emit(OpCodes.Ldloc, _exceptionInfo);
                IL.Emit(OpCodes.Callvirt, getReturnValue);
                IL.Emit(OpCodes.Stloc, _returnValue);
                IL.Append(returnOriginalValue);

                IL.Emit(OpCodes.Ldloc, _returnValue);
            }

            IL.Emit(OpCodes.Ret);
        }

        /// <summary>
        ///     Saves the current <see cref="IExceptionHandlerInfo" /> instance.
        /// </summary>
        /// <param name="targetMethod">The target method.</param>
        /// <param name="emitter">The <see cref="IEmitInvocationInfo" /> instance that will emit the current method context.</param>
        private void SaveExceptionInfo(MethodDefinition targetMethod, IEmitInvocationInfo emitter)
        {
            var IL = targetMethod.GetILGenerator();
            var module = IL.GetModule();

            emitter.Emit(targetMethod, targetMethod, _invocationInfo);
            IL.Emit(OpCodes.Ldloc, _exception);
            IL.Emit(OpCodes.Ldloc, _invocationInfo);

            var exceptionInfoConstructor = module.ImportConstructor<ExceptionHandlerInfo>(
                typeof(Exception),
                typeof(IInvocationInfo));
            IL.Emit(OpCodes.Newobj, exceptionInfoConstructor);
            IL.Emit(OpCodes.Stloc, _exceptionInfo);

            var returnType = targetMethod.ReturnType;
            if (returnType == _voidType || _returnValue == null)
                return;

            // exceptionInfo.ReturnValue = returnValue;
            var setReturnValue = module.ImportMethod<IExceptionHandlerInfo>("set_ReturnValue");
            IL.Emit(OpCodes.Ldloc, _exceptionInfo);
            IL.Emit(OpCodes.Ldloc, _returnValue);
            IL.Emit(OpCodes.Callvirt, setReturnValue);
        }

        /// <summary>
        ///     Determines whether or not the current item should be modified.
        /// </summary>
        /// <param name="item">The target item.</param>
        /// <returns>Returns <c>true</c> if the current item can be modified; otherwise, it should return <c>false.</c></returns>
        public bool ShouldWeave(MethodDefinition item)
        {
            return base.ShouldRewrite(item);
        }

        /// <summary>
        ///     Modifies the target <paramref name="item" />.
        /// </summary>
        /// <param name="item">The item to be modified.</param>
        public void Weave(MethodDefinition item)
        {
            Rewrite(item, item.GetILGenerator(), item.Body.Instructions.ToArray());
        }
    }
}