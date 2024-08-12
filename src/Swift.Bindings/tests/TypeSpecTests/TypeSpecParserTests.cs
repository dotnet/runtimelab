using Xunit;

namespace BindingsGeneration.Tests;

public class TypeSpecParserTests : IClassFixture<TypeSpecParserTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public TypeSpecParserTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestFixture
    {
        static TestFixture()
        {
        }

        private static void InitializeResources()
        {
        }
    }

    [Fact]
    public static void TestNamedBasicName()
    {
        var ts = TypeSpecParser.Parse("thisIsAName");
        var ns = ts as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("thisIsAName", ns.Name);
    }

    [Fact]
    public static void TestNamedGeneric()
    {
        var ts = TypeSpecParser.Parse("thisIsAName<a, b, c>");
        var ns = ts as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("thisIsAName", ns.Name);
        Assert.Equal(3, ns.GenericParameters.Count);
        var ns1 = ns.GenericParameters[0] as NamedTypeSpec;
        Assert.NotNull(ns1);
        Assert.Equal("a", ns1.Name);
        ns1 = ns.GenericParameters[1] as NamedTypeSpec;
        Assert.NotNull(ns1);
        Assert.Equal("b", ns1.Name);
        ns1 = ns.GenericParameters[2] as NamedTypeSpec;
        Assert.NotNull(ns1);
        Assert.Equal("c", ns1.Name);
    }

    [Fact]
    public static void TestEmptyTuple()
    {
        var tuple = TypeSpecParser.Parse("()") as TupleTypeSpec;
        Assert.NotNull(tuple);
        Assert.Empty(tuple.Elements);
    }

    [Fact]
    public static void TestSingleTuple()
    {
        var ns = TypeSpecParser.Parse("Swift.Int") as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("Swift.Int", ns.Name);
    }

    [Fact]
    public static void TestDoubleTuple()
    {
        var tuple = TypeSpecParser.Parse("(Swift.Int, Swift.Float)") as TupleTypeSpec;
        Assert.NotNull(tuple);
        Assert.Equal(2, tuple.Elements.Count);
        var ns = tuple.Elements[0] as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("Swift.Int", ns.Name);
        ns = tuple.Elements[1] as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("Swift.Float", ns.Name);
    }

    [Fact]
    public static void TestNestedTuple()
    {
        var tuple = TypeSpecParser.Parse("(Swift.Int, (Swift.Int, Swift.Int))") as TupleTypeSpec;
        Assert.NotNull(tuple);
        Assert.Equal(2, tuple.Elements.Count);
        var ns = tuple.Elements[0] as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("Swift.Int", ns.Name);
        tuple = tuple.Elements[1] as TupleTypeSpec;
        Assert.NotNull(tuple);
        Assert.Equal(2, tuple.Elements.Count);
        ns = tuple.Elements[0] as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("Swift.Int", ns.Name);
        ns = tuple.Elements[1] as NamedTypeSpec;
        Assert.NotNull(ns);
        Assert.Equal("Swift.Int", ns.Name);
    }

    [Fact]
    public static void TestFuncIntInt()
    {
        var close = TypeSpecParser.Parse ("Swift.Int -> Swift.Int") as ClosureTypeSpec;
        Assert.NotNull (close);
        var ns = close.Arguments as NamedTypeSpec;
        Assert.NotNull (ns);
        Assert.Equal ("Swift.Int", ns.Name);
        ns = close.ReturnType as NamedTypeSpec;
        Assert.NotNull (ns);
        Assert.Equal ("Swift.Int", ns.Name);
    }


    [Fact]
    public static void TestFuncVoidVoid()
    {
        var close = TypeSpecParser.Parse ("() -> ()") as ClosureTypeSpec;
        Assert.NotNull (close);
        var ts = close.Arguments as TupleTypeSpec;
        Assert.NotNull (ts);
        Assert.Empty (ts.Elements);
        ts = close.ReturnType as TupleTypeSpec;
        Assert.NotNull (ts);
        Assert.Empty (ts.Elements);
    }

    [Fact]
    public static void TestArrayOfInt()
    {
        var ns = TypeSpecParser.Parse ("Swift.Array<Swift.Int>") as NamedTypeSpec;
        Assert.NotNull (ns);
        Assert.Equal ("Swift.Array", ns.Name);
        Assert.True (ns.ContainsGenericParameters);
        Assert.Single (ns.GenericParameters);
        ns = ns.GenericParameters [0] as NamedTypeSpec;
        Assert.NotNull (ns);
        Assert.Equal ("Swift.Int", ns.Name);
    }

    [Fact]
    public static void TestDictionaryOfIntString()
    {
        var ns = TypeSpecParser.Parse ("Swift.Dictionary<Swift.Int, Swift.String>") as NamedTypeSpec;
        Assert.NotNull (ns);
        Assert.Equal ("Swift.Dictionary", ns.Name);
        Assert.True (ns.ContainsGenericParameters);
        Assert.Equal (2, ns.GenericParameters.Count);
        var ns1 = ns.GenericParameters [0] as NamedTypeSpec;
        Assert.NotNull (ns1);
        Assert.Equal ("Swift.Int", ns1.Name);
        ns1 = ns.GenericParameters [1] as NamedTypeSpec;
        Assert.NotNull (ns1);
        Assert.Equal ("Swift.String", ns1.Name);
    }

    [Fact]
    public static void TestWithAttributes ()
    {
        var tupled = TypeSpecParser.Parse ("(Builtin.RawPointer, (@convention[thin] (Builtin.RawPointer, inout Builtin.UnsafeValueBuffer, inout SomeModule.Foo, @thick SomeModule.Foo.Type) -> ())?)")
            as TupleTypeSpec;
        Assert.NotNull (tupled);
        var ns = tupled.Elements [1] as NamedTypeSpec;
        Assert.True (ns.ContainsGenericParameters);
        Assert.Equal ("Swift.Optional", ns.Name);
        var close = ns.GenericParameters[0] as ClosureTypeSpec;
        Assert.Single (close.Attributes);
    }

    [Fact]
    public static void TestEmbeddedClass()
    {
        var ns = TypeSpecParser.Parse ("Swift.Dictionary<Swift.String, T>.Index") as NamedTypeSpec;
        Assert.NotNull (ns);
        Assert.NotNull (ns.InnerType);
        Assert.Equal ("Index", ns.InnerType.Name);
        Assert.Equal ("Swift.Dictionary<Swift.String, T>.Index", ns.ToString ());
    }

    [Fact]
    public static void TestProtocolListAlphabetical ()
    {
        var specs = new NamedTypeSpec [] {
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Dfoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var protos = new ProtocolListTypeSpec (specs);
        Assert.Equal ("Afoo & Bfoo & Cfoo & Dfoo", protos.ToString ());
    }

    [Fact]
    public static void TestProtocolListParseSimple ()
    {
        var protocolListType = TypeSpecParser.Parse ("c & b & a") as ProtocolListTypeSpec;
        Assert.NotNull (protocolListType);
        Assert.Equal (3, protocolListType.Protocols.Count);
        Assert.Equal ("a & b & c", protocolListType.ToString ());
    }

    [Fact]
    public static void TestProtocolListParseNoSpacesBecauseWhyNot ()
    {
        var protocolListType = TypeSpecParser.Parse ("c&b&a") as ProtocolListTypeSpec;
        Assert.NotNull (protocolListType);
        Assert.Equal (3, protocolListType.Protocols.Count);
        Assert.Equal ("a & b & c", protocolListType.ToString ());
    }

    [Fact]
    public static void TestReplaceInNameSuccess ()
    {
        var inType = TypeSpecParser.Parse ("Foo.Bar");
        var replaced = inType.ReplaceName ("Foo.Bar", "Slarty.Bartfast") as NamedTypeSpec;
        Assert.NotNull (replaced);
        Assert.Equal ("Slarty.Bartfast", replaced.Name);
    }

    [Fact]
    public static void TestReplaceInNameFail ()
    {
        var inType = TypeSpecParser.Parse ("Foo.Bar");
        var same = inType.ReplaceName ("Blah", "Slarty.Bartfast") as NamedTypeSpec;
        Assert.Equal (same, inType);
    }

    [Fact]
    public static void TestReplaceInTupleSuccess ()
    {
        var inType = TypeSpecParser.Parse ("(Swift.Int, Foo.Bar, Foo.Bar)");
        var replaced = inType.ReplaceName ("Foo.Bar", "Slarty.Bartfast") as TupleTypeSpec;
        Assert.NotNull (replaced);
        var name = replaced.Elements [1] as NamedTypeSpec;
        Assert.NotNull (name);
        Assert.Equal ("Slarty.Bartfast", name.Name);
        name = replaced.Elements [2] as NamedTypeSpec;
        Assert.NotNull (name);
        Assert.Equal ("Slarty.Bartfast", name.Name);
    }

    [Fact]
    public static void TestReplaceInTupleFail ()
    {
        var inType = TypeSpecParser.Parse ("(Swift.Int, Foo.Bar, Foo.Bar)");
        var same = inType.ReplaceName ("Blah", "Slarty.Bartfast") as TupleTypeSpec;
        Assert.Equal (same, inType);
    }


    [Fact]
    public static void TestReplaceInClosureSuccess ()
    {
        var inType = TypeSpecParser.Parse ("(Swift.Int, Foo.Bar) -> Foo.Bar");
        var replaced = inType.ReplaceName ("Foo.Bar", "Slarty.Bartfast") as ClosureTypeSpec;
        Assert.NotNull (replaced);
        var args = replaced.Arguments as TupleTypeSpec;
        Assert.NotNull (args);
        Assert.Equal (2, args.Elements.Count);
        var name = args.Elements [1] as NamedTypeSpec;
        Assert.Equal ("Slarty.Bartfast", name.Name);
        name = replaced.ReturnType as NamedTypeSpec;
        Assert.Equal ("Slarty.Bartfast", name.Name);
    }

    [Fact]
    public static void TestReplaceInClosureFail ()
    {
        var inType = TypeSpecParser.Parse ("(Swift.Int, Foo.Bar) -> Foo.Bar");
        var same = inType.ReplaceName ("Blah", "Slarty.Bartfast") as ClosureTypeSpec;
        Assert.NotNull (same);
        Assert.Equal (same, inType);
    }

    [Fact]
    public static void TestReplaceInProtoListSuccess ()
    {
        var inType = TypeSpecParser.Parse ("Swift.Equatable & Foo.Bar");
        var replaced = inType.ReplaceName ("Foo.Bar", "Slarty.Bartfast") as ProtocolListTypeSpec;
        Assert.NotNull (replaced);
        var name = replaced.Protocols.Keys.FirstOrDefault (n => n.Name == "Slarty.Bartfast");
        Assert.NotNull (name);
    }

    [Fact]
    public static void TestReplaceInProtoListFail ()
    {
        var inType = TypeSpecParser.Parse ("Swift.Equatable & Foo.Bar");
        var same = inType.ReplaceName ("Blah", "Slarty.Bartfast") as ProtocolListTypeSpec;
        Assert.Equal (same, inType);
    }

    [Fact]
    public static void TestWeirdClosureIssue ()
    {
        var inType = TypeSpecParser.Parse ("@escaping[] (_onAnimation:Swift.Bool)->Swift.Void");
        Assert.True (inType is ClosureTypeSpec);
        var closSpec = inType as ClosureTypeSpec;
        Assert.True (closSpec.IsEscaping);
        var textRep = closSpec.ToString ();
        var firstIndex = textRep.IndexOf ("_onAnimation");
        var lastIndex = textRep.LastIndexOf ("_onAnimation");
        Assert.True (firstIndex == lastIndex);
    }

    [Fact]
    public static void TestAsyncClosure ()
    {
        var inType = TypeSpecParser.Parse ("() async -> ()") as ClosureTypeSpec;
        Assert.NotNull (inType);
        Assert.True (inType.IsAsync);
        Assert.False (inType.Throws);
    }

    [Fact]
    public static void TestAsyncThrowsClosure ()
    {
        var inType = TypeSpecParser.Parse ("() async throws -> ()") as ClosureTypeSpec;
        Assert.NotNull (inType);
        Assert.True (inType.IsAsync);
        Assert.True (inType.Throws);
    }

    [Fact]
    public static void TestThrowsClosure ()
    {
        var inType = TypeSpecParser.Parse ("() throws -> ()") as ClosureTypeSpec;
        Assert.NotNull (inType);
        Assert.False (inType.IsAsync);
        Assert.True (inType.Throws);
    }

    [Fact]
    public static void TestThrowBadArrow()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("(Swift.Int)-=>(Swift.Int)"); });
    }

    [Fact]
    public static void TestIllegalNameChar()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("Swift#Int"); });
    }

    [Fact]
    public static void TestBadStartToken()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse(")"); });
    }

    [Fact]
    public static void TestBadClosureToken()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("() throws ? -> )"); });
    }

    [Fact]
    public static void TestInnerClass1()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("().Foo"); });
    }
    
    [Fact]
    public static void TestProtoListFail()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("Foo & ()"); });
    }
    
    [Fact]
    public static void TestAttributeFail()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("@&Foo"); });
    }
    
    [Fact]
    public static void TestListFail()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("Swift.Foo<A, &>"); });
    }
    
    [Fact]
    public static void TestArrayFail1()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("[&]"); });
    }
    
    [Fact]
    public static void TestArrayFail2()
    {
        Assert.Throws<Exception>(() => { TypeSpecParser.Parse("[Swift.Int : ?]"); });
    }
}