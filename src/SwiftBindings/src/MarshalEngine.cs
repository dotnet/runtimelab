// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SyntaxDynamo.CSLang;
using SwiftReflector.TypeMapping;
using SyntaxDynamo;
using SwiftReflector.SwiftXmlReflection;

namespace SwiftReflector
{
    public class MarshalEngine
    {
        CSUsingPackages use;
        TypeMapper typeMapper;
        public bool skipThisParameterPremarshal = false;
        List<CSFixedCodeBlock> fixedChain = new List<CSFixedCodeBlock>();
        Version swiftLangVersion;
        public Func<int, int, string> genericReferenceNamer = null;
        public Func<int, int, string> GenericReferenceNamer { get; set; }

        public MarshalEngine(CSUsingPackages use, List<string> identifiersUsed, TypeMapper typeMapper, Version swiftLangVersion)
        {
            this.use = use;

            this.typeMapper = typeMapper;
            this.swiftLangVersion = swiftLangVersion;
        }

        public IEnumerable<ICodeElement> MarshalFunctionCall(FunctionDeclaration decl, CSMethod caller, CSMethod callee)
        {
            ICodeElement functionCall;
            var parms = new CSParameterList(caller.Parameters);
            var callParameters = new List<CSBaseExpression>(parms.Count);
            if (parms.Count > 0) {
                var filteredTypeSpec = FilterParams (parms, decl);
                for (int i = 0; i < parms.Count; i++)
                {
                    var p = parms[i];
                    var originalParm = decl.ParameterLists.Last()[i].TypeSpec;
                    callParameters.Add(Marshal(null, decl, p, filteredTypeSpec[i], false,
                                false, originalParm));
                }
            }

            var call = new CSFunctionCall(callee.Name.ToString(), false, callParameters.ToArray());
            if (decl.ReturnTypeSpec.Kind != TypeSpecKind.Tuple)
                functionCall = CSReturn.ReturnLine((ICSExpression)call);
            else
                functionCall = new CSLine(call);
           
            yield return functionCall;
        }

        CSParameter ReworkParameterWithNamer(CSParameter p)
        {
            if (GenericReferenceNamer == null)
                return p;
            var pClone = ReworkTypeWithNamer(p.CSType);
            return new CSParameter(pClone, p.Name, p.ParameterKind, p.DefaultValue);
        }

        CSType ReworkTypeWithNamer(CSType ty)
        {
            if (ty is CSGenericReferenceType genRef)
            {
                var newGen = new CSGenericReferenceType(genRef.Depth, genRef.Index);
                newGen.ReferenceNamer = GenericReferenceNamer;
                return newGen;
            }
            else if (ty is CSSimpleType simple)
            {
                if (simple.GenericTypes == null)
                    return simple;
                var genSubTypes = new CSType[simple.GenericTypes.Length];
                for (int i = 0; i < genSubTypes.Length; i++)
                {
                    genSubTypes[i] = ReworkTypeWithNamer(simple.GenericTypes[i]);
                }
                var simpleClone = new CSSimpleType(simple.GenericTypeName, simple.IsArray, genSubTypes);
                return simpleClone;
            }
            else
            {
                throw new NotImplementedException($"Unable to rework type {ty.GetType().Name} {ty.ToString()} as generic reference");
            }
        }

        CSBaseExpression Marshal(BaseDeclaration typeContext, FunctionDeclaration funcDecl, CSParameter p, TypeSpec swiftType,
            bool marshalProtocolAsValueType, bool isReturnVariable, TypeSpec originalType)
        {
            p = ReworkParameterWithNamer(p);

            var entityType = typeMapper.GetEntityTypeForTypeSpec(swiftType);
            switch (entityType)
            {
                case EntityType.Scalar:
                    return MarshalScalar (p);
                case EntityType.Tuple:
                case EntityType.None:
                    // Add more types
                    break;
            }
            throw new NotImplementedException($"Uh-oh - not ready for {swiftType.ToString()}, a {entityType}.");
        }

        CSBaseExpression MarshalScalar (CSParameter p)
		{
			return ParmName (p);
		}

        static CSIdentifier ParmName (CSParameter parm)
		{
			return ParmName (parm.Name.Name, parm.ParameterKind);
		}

		static CSIdentifier ParmName (string ident, CSParameterKind parmKind)
		{
			string prefix = "";
			switch (parmKind) {
			case CSParameterKind.Out:
				prefix = "out ";
				break;
			case CSParameterKind.Ref:
				prefix = "ref ";
				break;
			default:
				break;
			}
			return new CSIdentifier (String.Format ("{0}{1}", prefix, ident));
		}

        TypeSpec[] FilterParams(CSParameterList parms, FunctionDeclaration decl)
        {
            var results = new TypeSpec[parms.Count];
            var parameterList = decl.ParameterLists.Last();
            for (int i = 0; i < parms.Count; i++)
            {
                var currType = parameterList[i].TypeSpec;
                results[i] = currType;
            }
            return results;
        }
    }
}

