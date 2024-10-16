// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis.Wasm;
using ILCompiler.DependencyAnalysisFramework;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class WasmPreciseVirtualUnwindInfoNode : ObjectNode, ISymbolDefinitionNode, INodeWithSize
    {
        private int? _size;

        int INodeWithSize.Size => _size.Value;

        public override bool IsShareable => false;

        public override ObjectNodeSection GetSection(NodeFactory factory) => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;
        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.WasmPreciseVirtualUnwindInfoNode; // Must be after the stack trace mappings.

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__OutlinedPreciseVirtualUnwindInfoData"u8);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            if (relocsOnly)
            {
                return new ObjectData([], [], 1, [this]); // This is a summary node that has no dependencies of its own.
            }

            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);

            // Most of the unwind data is recorded "inline" in the stack trace info, to save size. Emit the rest.
            foreach (IWasmMethodCodeNode methodNode in factory.MetadataManager.GetCompiledMethodBodies())
            {
                if (methodNode is not { PreciseVirtualUnwindInfo.Emitted: true })
                {
                    WasmMethodPreciseVirtualUnwindInfoNode.Emit(methodNode, ref builder, factory);
                }
            }

            _size = builder.CountBytes;
            return builder.ToObjectData();
        }
    }

    public sealed class WasmMethodPreciseVirtualUnwindInfoNode(
        IWasmMethodCodeNode methodNode, uint shadowFrameSize, ObjectData ehInfo, bool hasStackTraceIp) : DependencyNodeCore<NodeFactory>, ISymbolDefinitionNode
    {
        private readonly IWasmMethodCodeNode _methodNode = methodNode;
        private readonly uint _shadowFrameSize = shadowFrameSize;
        private readonly ObjectData _ehInfo = ehInfo;
        private readonly bool _hasStackTraceIp = hasStackTraceIp;
        private int? _offset;

        private unsafe void EmitData(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            Debug.Assert(!Emitted);

            const uint HasExtendedInfoFlag = 1 << 7;
            const uint HasFunctionPointerFlag = 1 << 6;
            const uint AllFlags = HasExtendedInfoFlag | HasFunctionPointerFlag;
            const uint SmallShadowFrameSizeInSlotsLimit = byte.MaxValue & ~AllFlags;
            const uint IsHiddenExtendedFlag = 1 << 7;
            const uint SmallEHInfoSizeLimit = byte.MaxValue & ~IsHiddenExtendedFlag;

            _offset = builder.CountBytes;
            if (_shadowFrameSize != 0)
            {
                builder.AddSymbol(this); // Only instances used for actual virtual unwinding define symbols.
            }

            Debug.Assert(_shadowFrameSize % builder.TargetPointerSize == 0);
            uint shadowFrameSizeInSlots = _shadowFrameSize / (uint)builder.TargetPointerSize;
            ObjectData ehInfo = _ehInfo;

            // At runtime, we may need to map from a function pointer to "the IP", i. e. the address of this unwind
            // info, as well as vice-versa:
            // 1. To construct the stack trace info from reflection data. Reflection tables are keyed on function
            //    pointers. This is the IP -> function pointer direction. Therefore, we store the function pointer
            //    for all methods that are part of the invoke map.
            // 2. To map a function pointer from a delegate to an IP, and then use that IP to construct stack trace
            //    info (DiagnosticMethodInfo.Create). This is the function pointer -> IP direction. The runtime will
            //    construct a reverse map for this (fairly uncommon) case.
            //
            bool hasFunctionPointer =
                _hasStackTraceIp && factory.MetadataManager.IsPossibleDelegateOrReflectionTarget(factory, _methodNode);

            // [small shadow frame size | flags] [large shadow frame size] [function pointer].
            uint smallShadowFrameSizeAndFlags = 0;
            if (hasFunctionPointer)
            {
                smallShadowFrameSizeAndFlags |= HasFunctionPointerFlag;
            }
            bool hasExtendedInfo = ehInfo != null || !_hasStackTraceIp;
            if (hasExtendedInfo)
            {
                smallShadowFrameSizeAndFlags |= HasExtendedInfoFlag;
            }
            if (shadowFrameSizeInSlots < SmallShadowFrameSizeInSlotsLimit)
            {
                smallShadowFrameSizeAndFlags |= shadowFrameSizeInSlots;
            }
            else
            {
                smallShadowFrameSizeAndFlags |= SmallShadowFrameSizeInSlotsLimit;
            }
            Debug.Assert(smallShadowFrameSizeAndFlags == (byte)smallShadowFrameSizeAndFlags);
            builder.EmitByte((byte)smallShadowFrameSizeAndFlags);
            if (shadowFrameSizeInSlots >= SmallShadowFrameSizeInSlotsLimit)
            {
                // Only a very small proportion (< 1%) of methods need to use this 'large' format.
                builder.EmitUInt(shadowFrameSizeInSlots);
            }
            if (hasFunctionPointer)
            {
                // For instance methods on value types, we want the unboxing stub as that's what reflection uses.
                // The delegate case ('2') may need to map back both entrypoints; that too requires access to
                // the unboxing version - we can map it to its target at runtime.
                MethodDesc method = _methodNode.Method;
                if (method.OwningType.IsValueType && !method.Signature.IsStatic)
                {
                    builder.EmitPointerReloc(factory.MethodEntrypoint(method, unboxingStub: true));
                }
                else
                {
                    builder.EmitPointerReloc(_methodNode);
                }
            }

            // [small EH info size | is hidden] [large EH info size] [EH info].
            if (hasExtendedInfo)
            {
                uint smallEHInfoSizeAndExtendedFlags = 0;
                if (!_hasStackTraceIp)
                {
                    smallEHInfoSizeAndExtendedFlags |= IsHiddenExtendedFlag;
                }
                uint ehInfoSize = ehInfo != null ? (uint)ehInfo.Data.Length : 0;
                if (ehInfoSize < SmallEHInfoSizeLimit)
                {
                    smallEHInfoSizeAndExtendedFlags |= ehInfoSize;
                }
                else
                {
                    smallEHInfoSizeAndExtendedFlags |= SmallEHInfoSizeLimit;
                }
                Debug.Assert(smallEHInfoSizeAndExtendedFlags == (byte)smallEHInfoSizeAndExtendedFlags);
                builder.EmitByte((byte)smallEHInfoSizeAndExtendedFlags);
                if (ehInfoSize >= SmallEHInfoSizeLimit)
                {
                    builder.EmitUInt(ehInfoSize);
                }
                if (ehInfoSize != 0)
                {
                    Debug.Assert(ehInfo.DefinedSymbols.AsSpan().IsEmpty);
                    foreach (ref Relocation reloc in ehInfo.Relocs.AsSpan())
                    {
                        builder.AddReloc(new Relocation(reloc.RelocType, builder.CountBytes + reloc.Offset, reloc.Target));
                    }
                    builder.EmitBytes(ehInfo.Data);
                }
            }
        }

        public int Offset => _offset.Value;
        public bool Emitted => _offset.HasValue;
        public bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            if (_ehInfo != null)
            {
                foreach (Relocation reloc in _ehInfo.Relocs)
                {
                    yield return new DependencyListEntry(reloc.Target, "Used by EH info");
                }
            }
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__unwind_info_"u8);
            sb.Append(nameMangler.GetMangledMethodName(_methodNode.Method));
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public static void Emit(IWasmMethodCodeNode methodNode, ref ObjectDataBuilder builder, NodeFactory factory)
        {
            Debug.Assert(factory.MarkingComplete);
            WasmMethodPreciseVirtualUnwindInfoNode unwindInfo = methodNode.PreciseVirtualUnwindInfo;
            if (unwindInfo == null && factory.MetadataManager.RequiresStackTraceIpWithPreciseVirtualUnwind(factory, methodNode))
            {
                unwindInfo = new WasmMethodPreciseVirtualUnwindInfoNode(methodNode, 0, null, hasStackTraceIp: true);
                methodNode.InitializePreciseVirtualUnwindInfo(unwindInfo);
            }

            unwindInfo?.EmitData(ref builder, factory);
        }

        public static uint GetUnwindInfoViaAbsoluteValueLimit(CompilerTypeSystemContext context)
        {
            // So that it cannot overlap with any valid pointers.
            return context.WasmGlobalBase;
        }

        public static WasmMethodPreciseVirtualUnwindInfoNode Create(
            NodeFactory factory, IWasmMethodCodeNode methodNode, uint shadowStackSize, ObjectData ehInfo, out uint absoluteValue)
        {
            // In the precise virtual unwinding model, we don't have anything meaningful (for symbolication) to display
            // in case the stack trace metadata is missing. We also don't want to add precise virtual unwind frames to
            // methods that won't have the aforementioned metadata if we can avoid doing so (an otherwise empty shadow
            // frame). Combined with the fact we don't want the behavior to vary with optimization level, this implies
            // **always** hiding methods without stack trace metadata from the stack trace APIs, which is why we encode
            // visibility into the unwind info.
            bool hasStackTraceIp = factory.MetadataManager.HasStackTraceIpWithPreciseVirtualUnwind(methodNode);
            if (!hasStackTraceIp && ehInfo == null)
            {
                // Optimization: we can encode a hidden frame without EH via an absolute value (the frame size).
                uint shadowStackSizeInSlots = shadowStackSize / (uint)factory.Target.PointerSize;
                if (shadowStackSizeInSlots < GetUnwindInfoViaAbsoluteValueLimit(factory.TypeSystemContext))
                {
                    absoluteValue = shadowStackSizeInSlots;
                    return null;
                }
            }

            absoluteValue = 0;
            return new WasmMethodPreciseVirtualUnwindInfoNode(methodNode, shadowStackSize, ehInfo, hasStackTraceIp);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
    }
}
