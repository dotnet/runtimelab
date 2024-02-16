// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;
using SyntaxDynamo.CSLang;
using SwiftReflector.ExceptionTools;
using SwiftReflector.TypeMapping;
using SwiftReflector.SwiftXmlReflection;
using SwiftRuntimeLibrary;
using SwiftReflector.Demangling;

namespace SwiftReflector
{
    public class TopLevelFunctionCompiler
    {
        TypeMapper typeMap;
        Dictionary<string, string> mangledToCSharp = new Dictionary<string, string>();

        public TopLevelFunctionCompiler(TypeMapper typeMap)
        {
            this.typeMap = typeMap;
        }

        public CSProperty CompileProperty(string propertyName, CSUsingPackages packs, SwiftType swiftPropertyType, bool hasGetter, bool hasSetter,
                         CSMethodKind methodKind)
        {
            propertyName = typeMap.SanitizeIdentifier(propertyName);
            NetTypeBundle propertyType = typeMap.MapType(swiftPropertyType, false);

            if (!(swiftPropertyType is SwiftGenericArgReferenceType))
                AddUsingBlock(packs, propertyType);
            ICodeElement[] uselessLine = new ICodeElement[] { CSReturn.ReturnLine(new CSIdentifier("useless")) };

            CSCodeBlock getterBlock = null;
            if (hasGetter)
                getterBlock = new CSCodeBlock(uselessLine);
            CSCodeBlock setterBlock = null;
            if (hasSetter)
                setterBlock = new CSCodeBlock(uselessLine);

            CSProperty theProp = new CSProperty(propertyType.ToCSType(packs), methodKind,
                new CSIdentifier(propertyName), CSVisibility.Public, getterBlock, CSVisibility.Public, setterBlock);
            if (getterBlock != null)
                getterBlock.Clear();
            if (setterBlock != null)
                setterBlock.Clear();

            return theProp;
        }

        public CSProperty CompileProperty(CSUsingPackages packs, string propertyName,
            FunctionDeclaration getter, FunctionDeclaration setter, CSMethodKind methodKind = CSMethodKind.None)
        {
            var swiftPropertyType = GetPropertyType(getter, setter);
            NetTypeBundle propertyType = null;
            if (TypeMapper.IsCompoundProtocolListType(swiftPropertyType))
            {
                propertyType = new NetTypeBundle("System", "object", false, false, EntityType.ProtocolList);
            }
            else
            {
                propertyType = typeMap.MapType(getter, swiftPropertyType, false, true);
            }
            propertyName = propertyName ?? typeMap.SanitizeIdentifier(getter != null ? getter.PropertyName : setter.PropertyName);
            bool isSubscript = getter != null ? getter.IsSubscript :
                setter.IsSubscript;

            if (!getter.IsTypeSpecGeneric(swiftPropertyType))
                AddUsingBlock(packs, propertyType);

            var uselessLine = new ICodeElement[] { CSReturn.ReturnLine(new CSIdentifier("useless")) };

            CSCodeBlock getterBlock = null;
            if (getter != null)
                getterBlock = new CSCodeBlock(uselessLine);
            CSCodeBlock setterBlock = null;
            if (setter != null)
                setterBlock = new CSCodeBlock(uselessLine);

            CSProperty theProp = null;
            var csPropType = propertyType.ToCSType(packs);
            if (isSubscript)
            {
                List<ParameterItem> swiftParms = null;
                if (getter != null)
                {
                    swiftParms = getter.ParameterLists[1];
                }
                else
                {
                    swiftParms = setter.ParameterLists[1].Skip(1).ToList();
                }
                var args = typeMap.MapParameterList(getter, swiftParms, false, false, null, null, packs);
                args.ForEach(a => AddUsingBlock(packs, a.Type));

                var csParams =
                    new CSParameterList(
                        args.Select(a =>
                            new CSParameter(a.Type.ToCSType(packs),
                                new CSIdentifier(a.Name), a.Type.IsReference ? CSParameterKind.Ref : CSParameterKind.None, null)));
                theProp = new CSProperty(csPropType, methodKind, CSVisibility.Public, getterBlock,
                    CSVisibility.Public, setterBlock, csParams);


            }
            else
            {
                theProp = new CSProperty(csPropType, methodKind,
                    new CSIdentifier(propertyName), CSVisibility.Public, getterBlock, CSVisibility.Public, setterBlock);
            }
            // if (propertyType.Throws)
            // 	DecoratePropWithThrows (theProp, packs);
            if (getterBlock != null)
                getterBlock.Clear();
            if (setterBlock != null)
                setterBlock.Clear();

            return theProp;
        }

        SwiftType GetPropertyType(SwiftPropertyType getter, SwiftPropertyType setter)
        {
            if (getter != null)
            {
                return getter.ReturnType;
            }
            if (setter != null)
            {
                if (setter.IsSubscript)
                {
                    return ((SwiftTupleType)setter.Parameters).Contents[0];
                }
                else
                {
                    return setter.Parameters;
                }
            }
            throw ErrorHelper.CreateError(ReflectorError.kCompilerBase + 0, "neither getter nor setter provided");
        }

        TypeSpec GetPropertyType(FunctionDeclaration getter, FunctionDeclaration setter)
        {
            if (getter != null)
            {
                return getter.ReturnTypeSpec;
            }
            if (setter != null)
            {
                // same for subscript and prop
                return setter.ParameterLists[1][0].TypeSpec;
            }
            throw ErrorHelper.CreateError(ReflectorError.kCompilerBase + 1, "neither getter nor setter provided");
        }

        public CSMethod CompileMethod(FunctionDeclaration func, CSUsingPackages packs, string libraryPath,
            string mangledName, string functionName, bool isPinvoke, bool isFinal, bool isStatic)
        {
            isStatic = isStatic || func.IsExtension;
            var extraProtoArgs = new CSGenericTypeDeclarationCollection();
            var extraProtoConstraints = new CSGenericConstraintCollection();
            var args = typeMap.MapParameterList(func, func.ParameterLists.Last(), isPinvoke, false, extraProtoArgs, extraProtoConstraints, packs);
            if (isPinvoke && func.ParameterLists.Count > 1)
            {
                var metaTypeBundle = new NetTypeBundle("SwiftRuntimeLibrary", "SwiftMetatype", false, false, EntityType.None);
                NetParam p = new NetParam("metaClass", metaTypeBundle);
                args.Add(p);
            }

            NetTypeBundle returnType = null;
            if (func.ReturnTypeSpec is ProtocolListTypeSpec plitem && !isPinvoke)
            {
                returnType = new NetTypeBundle("System", "object", false, false, EntityType.ProtocolList);
            }
            else
            {
                returnType = typeMap.MapType(func, func.ReturnTypeSpec, isPinvoke, true);
            }

            string funcName = functionName ?? typeMap.SanitizeIdentifier(func.Name);

            if (isPinvoke && !mangledToCSharp.ContainsKey(mangledName))
                mangledToCSharp.Add(mangledName, funcName);

            args.ForEach(a => AddUsingBlock(packs, a.Type));

            if (returnType != null && !(func.IsTypeSpecGeneric(func.ReturnTypeSpec)))
                AddUsingBlock(packs, returnType);

            CSType csReturnType = returnType.IsVoid ? CSSimpleType.Void : returnType.ToCSType(packs);

            var csParams = new CSParameterList();
            foreach (var arg in args)
            {
                var csType = arg.Type.ToCSType(packs);
                // if (arg.Type.Throws)
                // 	csType = DecorateTypeWithThrows (csType, packs);
                csParams.Add(new CSParameter(csType, new CSIdentifier(arg.Name),
                    arg.Type.IsReference ? CSParameterKind.Ref : CSParameterKind.None, null));

            }

            if (isPinvoke)
            {
                // AddExtraGenericArguments (func, csParams, packs);
                var pinvoke = CSMethod.InternalPInvoke(csReturnType, funcName, libraryPath,
                    mangledName.Substring(1), csParams);
                if (csReturnType is CSSimpleType simple && simple.Name == "bool")
                {
                    CSAttribute.ReturnMarshalAsI1.AttachBefore(pinvoke);
                }
                return pinvoke;
            }
            else
            {
                CSMethod retval = null;
                if (func.IsConstructor)
                {
                    retval = CSMethod.PublicConstructor(funcName, csParams, new CSCodeBlock());
                }
                else
                {
                    if (isFinal)
                        retval = new CSMethod(CSVisibility.Public, isStatic ? CSMethodKind.Static : CSMethodKind.None, csReturnType, new CSIdentifier(funcName),
                            csParams, new CSCodeBlock());
                    else
                        retval = new CSMethod(CSVisibility.Public, isStatic ? CSMethodKind.Static : CSMethodKind.Virtual, csReturnType, new CSIdentifier(funcName),
                            csParams, new CSCodeBlock());
                }
                if (extraProtoArgs.Count > 0)
                {
                    retval.GenericParameters.AddRange(extraProtoArgs);
                    retval.GenericConstraints.AddRange(extraProtoConstraints);
                }
                return retval;
            }
        }

        public static bool GenericArgumentIsReferencedByGenericClassInParameterList(SwiftBaseFunctionType func, GenericArgument arg)
        {
            foreach (SwiftType st in func.EachParameter)
            {
                if (st is SwiftUnboundGenericType)
                {
                    var sut = (SwiftUnboundGenericType)st;
                    if (!sut.DependentType.IsClass)
                        continue;
                    // there appears to be a bug in the swift compiler that doesn't accept certain
                    // generic patterns that will ensure that sut.Arguments won't ever have more than 1
                    // element in it in cases that we care about, but what the heck - do the general case.
                    foreach (GenericArgument gen in sut.Arguments)
                    {
                        if (gen.Depth == arg.Depth && gen.Index == arg.Index)
                            return true;
                    }
                }
            }
            return false;
        }

        public static bool GenericDeclarationIsReferencedByGenericClassInParameterList(FunctionDeclaration func, GenericDeclaration genDecl, TypeMapper mapper)
        {
            foreach (ParameterItem pi in func.ParameterLists.Last())
            {
                if (pi.TypeSpec is NamedTypeSpec)
                {
                    // this inner section should probably be recursive, but I was unable to
                    // even test if SomeClass<SomeOtherClass<T>> is valid because the swift compiler
                    // wouldn't take it.
                    var ns = (NamedTypeSpec)pi.TypeSpec;
                    if (ns.ContainsGenericParameters)
                    {
                        Entity en = mapper.GetEntityForTypeSpec(ns);
                        if (en != null && en.EntityType == EntityType.Class)
                        {
                            foreach (TypeSpec genTS in ns.GenericParameters)
                            {
                                var nsGen = genTS as NamedTypeSpec;
                                if (nsGen != null)
                                {
                                    if (genDecl.Name == nsGen.Name)
                                        return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        public string CSMethodForMangledName(string mangledName)
        {
            return mangledToCSharp[SwiftRuntimeLibrary.Exceptions.ThrowOnNull(mangledName, "mangledName")];
        }

        static void AddUsingBlock(CSUsingPackages packs, NetTypeBundle type)
        {
            if (type.IsVoid || String.IsNullOrEmpty(type.NameSpace))
                return;
            packs.AddIfNotPresent(type.NameSpace);
        }


        int TotalProtocolConstraints(GenericArgument gen)
        {
            int count = 0;
            foreach (var constraint in gen.Constraints)
            {
                var ct = constraint as SwiftClassType;
                if (ct == null)
                    throw ErrorHelper.CreateError(ReflectorError.kCompilerBase + 11, $"Expected a SwiftClassType for constraint, but got {constraint.GetType().Name}.");
                if (ct.EntityKind == MemberNesting.Protocol)
                    count++;
            }
            return count;
        }
        int TotalProtocolConstraints(GenericDeclaration gen)
        {
            int count = 0;
            foreach (BaseConstraint constraint in gen.Constraints)
            {
                var inh = constraint as InheritanceConstraint;
                if (inh == null)
                    continue;
                // throw ErrorHelper.CreateError (ReflectorError.kCompilerBase + 12, $"Expected a SwiftClassType for constraint, but got {constraint.GetType ().Name}.");
                var en = typeMap.GetEntityForTypeSpec(inh.InheritsTypeSpec);
                if (en.EntityType == EntityType.Protocol)
                    count++;
            }
            return count;
        }

        bool IsObjCStruct(NetParam ntb, SwiftType parmType)
        {
            if (ntb.Type.Entity != EntityType.Struct)
                return false;

            // if the Entity is EntityType.Struct, it's guaranteed to be a SwiftClassType
            var structType = parmType as SwiftClassType;
            var entity = typeMap.GetEntityForSwiftClassName(structType.ClassName.ToFullyQualifiedName(true));
            if (entity == null)
                throw ErrorHelper.CreateError(ReflectorError.kCompilerReferenceBase + 5, $"Unable to get the entity for struct type {structType.ClassName.ToFullyQualifiedName(true)}");
            return entity.Type.IsObjC;
        }

        bool IsObjCStruct(TypeSpec typeSpec)
        {
            if (!(typeSpec is NamedTypeSpec))
                return false;
            var entity = typeMap.GetEntityForTypeSpec(typeSpec);
            if (entity == null)
                throw ErrorHelper.CreateError(ReflectorError.kCompilerReferenceBase + 6, $"Unable to get the entity for type {typeSpec.ToString()}");
            return entity.IsObjCStruct;
        }

        void RemapSwiftClosureRepresensation(List<NetParam> args)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (args[i].Type.FullName == "SwiftRuntimeLibrary.SwiftClosureRepresentation")
                {
                    var bundle = new NetTypeBundle("SwiftRuntimeLibrary", "BlindSwiftClosureRepresentation", false, args[i].Type.IsReference, EntityType.Closure);
                    args[i] = new NetParam(args[i].Name, bundle);
                }
            }
        }

        public static bool TypeSpecCanThrow(TypeSpec t, bool isPinvoke)
        {
            if (t == null)
                return false;
            return !isPinvoke && t is ClosureTypeSpec cl && cl.Throws;
        }
    }
}

