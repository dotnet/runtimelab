// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Runtime.InteropServices;

namespace System.Reflection.Emit
{
    internal sealed class DynamicILGenerator : ILGenerator
    {
        internal DynamicILGenerator(DynamicMethod method, byte[] methodSignature, int size)
        {
            m_method = method;
            m_methodSignature = methodSignature;
            m_size = size;
        }

        #region Emit
        public override void Emit(OpCode opcode)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, byte arg)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, short arg)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, long arg)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, float arg)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, double arg)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, int arg)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, MethodInfo meth)
        {
            throw new PlatformNotSupportedException();
        }

        public override void EmitCalli(OpCode opcode, CallingConventions callingConvention,
            Type? returnType, Type[]? parameterTypes, Type[]? optionalParameterTypes)
        {
            throw new PlatformNotSupportedException();
        }

        public override void EmitCalli(OpCode opcode, CallingConvention unmanagedCallConv, Type? returnType, Type[]? parameterTypes)
        {
            throw new PlatformNotSupportedException();
        }

        public override void EmitCall(OpCode opcode, MethodInfo methodInfo, Type[]? optionalParameterTypes)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, SignatureHelper signature)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, ConstructorInfo con)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, Type cls)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, Label label)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, Label[] labels)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, FieldInfo field)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, string str)
        {
            throw new PlatformNotSupportedException();
        }

        public override void Emit(OpCode opcode, LocalBuilder local)
        {
            throw new PlatformNotSupportedException();
        }
        #endregion

        #region Exceptions
        public override Label BeginExceptionBlock()
        {
            throw new PlatformNotSupportedException();
        }

        public override void EndExceptionBlock()
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeginExceptFilterBlock()
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeginCatchBlock(Type? exceptionType)
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeginFaultBlock()
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeginFinallyBlock()
        {
            throw new PlatformNotSupportedException();
        }

        #endregion

        #region Labels
        public override Label DefineLabel()
        {
            throw new PlatformNotSupportedException();
        }

        public override void MarkLabel(Label loc)
        {
            throw new PlatformNotSupportedException();
        }

        #endregion

        #region Debug API

        public override LocalBuilder DeclareLocal(Type localType, bool pinned)
        {
            throw new PlatformNotSupportedException();
        }

        public override void UsingNamespace(string usingNamespace)
        {
            throw new PlatformNotSupportedException();
        }

        public override void BeginScope()
        {
            throw new PlatformNotSupportedException();
        }

        public override void EndScope()
        {
            throw new PlatformNotSupportedException();
        }

        public override int ILOffset => throw new PlatformNotSupportedException();

        #endregion

        DynamicMethod m_method;
        byte[] m_methodSignature;
        int m_size;
    }
}
