// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Reflection.Emit
{
    // FIXME: ivanpovazan - mostly copied stuff from CoreCLR's RuntimeILGenerator to make things work
    internal sealed class DynamicILGenerator : ILGenerator
    {
        internal DynamicILGenerator(DynamicMethod method, byte[] methodSignature, int size)
        {
            m_method = method;
            m_methodSignature = methodSignature;
            m_length = 0;
            m_ILStream = new byte[Math.Max(size, DefaultSize)];
        }

        internal void InternalEmit(OpCode opcode)
        {
            short opcodeValue = opcode.Value;
            if (opcode.Size != 1)
            {
                BinaryPrimitives.WriteInt16BigEndian(m_ILStream.AsSpan(m_length), opcodeValue);
                m_length += 2;
            }
            else
            {
                m_ILStream[m_length++] = (byte)opcodeValue;
            }

            UpdateStackSize(opcode, opcode.StackChange());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateStackSize(OpCode opcode, int stackchange)
        {
            // Updates internal variables for keeping track of the stack size
            // requirements for the function.  stackchange specifies the amount
            // by which the stacksize needs to be updated.

            if (m_curDepth < 0)
            {
                // Current depth is "unknown". We get here when:
                // * this is unreachable code.
                // * the client uses explicit numeric offsets rather than Labels.
                m_curDepth = 0;
            }

            m_curDepth += stackchange;
            if (m_curDepth < 0)
            {
                // Stack underflow. Assume our previous depth computation was flawed.
                m_depthAdjustment -= m_curDepth;
                m_curDepth = 0;
            }
            else if (m_maxDepth < m_curDepth)
                m_maxDepth = m_curDepth;
            Debug.Assert(m_depthAdjustment >= 0);
            Debug.Assert(m_curDepth >= 0);

            // Record the stack depth at a "target" of this instruction.
            m_targetDepth = m_curDepth;

            // If the current instruction can't fall through, set the depth to unknown.
            if (opcode.EndsUncondJmpBlk())
                m_curDepth = -1;
        }

        internal void EnsureCapacity(int size)
        {
            // Guarantees an array capable of holding at least size elements.
            if (m_length + size >= m_ILStream.Length)
            {
                IncreaseCapacity(size);
            }
        }

        private void IncreaseCapacity(int size)
        {
            byte[] temp = new byte[Math.Max(m_ILStream.Length * 2, m_length + size)];
            Array.Copy(m_ILStream, temp, m_ILStream.Length);
            m_ILStream = temp;
        }

        #region Emit
        public override void Emit(OpCode opcode)
        {
            EnsureCapacity(3);
            InternalEmit(opcode);
        }

        public override void Emit(OpCode opcode, byte arg)
        {
            EnsureCapacity(4);
            InternalEmit(opcode);
            m_ILStream[m_length++] = arg;
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

        private const int DefaultSize = 16;

        DynamicMethod m_method;
        byte[] m_methodSignature;
        private int m_length;
        private byte[] m_ILStream;
        private int m_curDepth; // Current stack depth, with -1 meaning unknown.
        private int m_targetDepth; // Stack depth at a target of the previous instruction (when it is branching).
        private int m_maxDepth; // Running max of the stack depth.

        // Adjustment to add to m_maxDepth for incorrect/invalid IL. For example, when branch instructions
        // with different stack depths target the same label.
        private long m_depthAdjustment;
    }
}
