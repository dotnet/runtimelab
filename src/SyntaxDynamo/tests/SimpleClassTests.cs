// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using SyntaxDynamo.CSLang;
using System.Reflection;
using TestingUtils;

namespace SyntaxDynamo.Tests;

public class SimpleClassTests
{
	public delegate CSClass ClassMutator(CSClass cl);
	public delegate CSUsingPackages UsingMutator(CSUsingPackages pkg);

	public static Stream BasicClass(string nameSpace, string className, CSMethod m, ClassMutator mutator, UsingMutator useMutator = null)
	{
		CSUsingPackages use = new CSUsingPackages("System");
		if(useMutator != null)
			use = useMutator(use);

		CSClass cl = new CSClass(CSVisibility.Public, className, m != null ? new CSMethod [] { m } : null);
		if(mutator != null)
			cl = mutator(cl);

		CSNamespace ns = new CSNamespace(nameSpace);
		ns.Block.Add(cl);

		CSFile file = CSFile.Create(use, ns);
		return CodeWriter.WriteToStream(file);
	}

    [Fact]
    public void EmptyClass()
    {
        using(Stream stm = BasicClass("None", "AClass", null, null)) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    void DeclExposure(CSVisibility vis)
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Fields.Add(CSFieldDeclaration.FieldLine(CSSimpleType.Byte, "b", null, CSVisibility.Public));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void ClassWithSingleDeclAllExposures()
    {
        foreach(var vis in Enum.GetValues(typeof(CSVisibility))) {
            DeclExposure((CSVisibility)vis);
        }
    }

    void DeclInitExposure(CSVisibility vis)
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Fields.Add(CSFieldDeclaration.FieldLine(CSSimpleType.Byte, "b", CSConstant.Val((byte)0), CSVisibility.Public));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void ClassWithSingleDeclInitAllExposures()
    {
        foreach(var vis in Enum.GetValues(typeof(CSVisibility))) {
            DeclInitExposure((CSVisibility)vis);
        }
    }

    void DeclType(CSType type)
    {
        if(type == CSSimpleType.Void)
            return;

        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Fields.Add(CSFieldDeclaration.FieldLine(type, "b", null, CSVisibility.Public));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void ClassWithSingleDeclAllTypes()
    {
        foreach(MethodInfo mi in typeof(CSType).GetMethods().Where(mii => mii.IsStatic && mii.IsPublic &&
                mii.ReturnType == typeof(CSType) && mii.Name != "Copy")) {
            CSType cs = mi.Invoke(null, null) as CSType;
            if(cs != null)
                DeclType(cs);
        }
    }

    [Fact]
    public void ConstructorTest()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Constructors.Add(CSMethod.PublicConstructor("AClass", new CSParameterList(), new CSCodeBlock()));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void ConstructorTestParam()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, new CSIdentifier("x")));
            cl.Constructors.Add(CSMethod.PublicConstructor("AClass", pl, new CSCodeBlock()));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void ConstructorTestParamList()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"))
                .And(new CSParameter(CSSimpleType.Int, "y"));
            cl.Constructors.Add(CSMethod.PublicConstructor("AClass", pl, new CSCodeBlock()));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void MethodNoParams()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList();
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void MethodParam()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"));
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void MethodParamList()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"))
                .And(new CSParameter(CSSimpleType.Int, "y"));
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void VirtualMethodNoParams()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList();
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSMethodKind.Virtual, CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void VirtualMethodParam()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"));
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSMethodKind.Virtual, CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void VirtualMethodParamList()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"))
                .And(new CSParameter(CSSimpleType.Int, "y"));
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSMethodKind.Virtual, CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void StaticMethodNoParams()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList();
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSMethodKind.Virtual, CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void StaticMethodParam()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"));
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSMethodKind.Virtual, CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void StaticMethodParamList()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            CSParameterList pl = new CSParameterList().And(new CSParameter(CSSimpleType.Int, "x"))
                .And(new CSParameter(CSSimpleType.Int, "y"));
            CSCodeBlock b = new CSCodeBlock().And(CSReturn.ReturnLine(CSConstant.Val(0)));
            cl.Methods.Add(CSMethod.PublicMethod(CSMethodKind.Virtual, CSSimpleType.Int, "Foo", pl, b));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void PublicGetPrivateSetProp()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Properties.Add(CSProperty.PublicGetPrivateSet(CSSimpleType.Int, "Foo"));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void PublicGetSetProp()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Properties.Add(CSProperty.PublicGetSet(CSSimpleType.Int, "Foo"));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void PublicGetSetBacking()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Properties.Add(CSProperty.PublicGetSetBacking(CSSimpleType.Int, "Foo", true, "_bar"));
            return cl;
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }

    [Fact]
    public void Pinvoke()
    {
        using(Stream stm = BasicClass("None", "AClass", null, cl => {
            cl.Methods.Add(CSMethod.PInvoke(CSVisibility.Public,
                CSSimpleType.IntPtr, "Walter", "__Internal", "_walter", new CSParameterList()));
            return cl;
        }, use => {
            return use.And(new CSUsing("System.Runtime.InteropServices"));
        })) {
            TestHelpers.CompileAndExecute(stm, string.Empty, string.Empty);
        }
    }
}
