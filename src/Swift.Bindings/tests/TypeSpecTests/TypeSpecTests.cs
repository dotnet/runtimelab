using Xunit;

namespace BindingsGeneration.Tests;

public class TypeSpecTests : IClassFixture<TypeSpecTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public TypeSpecTests(TestFixture fixture)
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
        var named = new NamedTypeSpec("Name");
        Assert.Equal("Name", named.Name);
    }

    [Fact]
    public static void TestNamedTickedName()
    {
        var named = new NamedTypeSpec("`Name`");
        Assert.Equal("Name", named.Name);
        Assert.Equal("Name", named.ToString());
    }


    [Fact]
    public static void TestNamedAny()
    {
        var named = new NamedTypeSpec("Any");
        Assert.Equal("Swift.Any", named.Name);
    }


    [Fact]
    public static void TestNamedAnyObject()
    {
        var named = new NamedTypeSpec("AnyObject");
        Assert.Equal("Swift.AnyObject", named.Name);
        Assert.True(named.HasModule());
        Assert.Equal("AnyObject", named.NameWithoutModule);
        Assert.Equal("Swift", named.Module);
    }

    [Fact]
    public static void TestEmptyTuple()
    {
        var tuple = TupleTypeSpec.Empty;
        Assert.Empty(tuple.Elements);
        Assert.True(tuple.IsEmptyTuple);
    }

    [Fact]
    public static void TestSingleTuple()
    {
        var tuple = new TupleTypeSpec(new NamedTypeSpec("name"));
        Assert.Single(tuple.Elements);
        Assert.True(tuple.Elements[0] is NamedTypeSpec);
    }

    static IEnumerable<TypeSpec> GetSomeTypes()
    {
        yield return new NamedTypeSpec("a");
        yield return new NamedTypeSpec("b");
        yield return new NamedTypeSpec("c");
    }

    static IEnumerable<NamedTypeSpec> GetSomeNamedTypes()
    {
        yield return new NamedTypeSpec("a");
        yield return new NamedTypeSpec("b");
        yield return new NamedTypeSpec("c");
    }

    static IEnumerable<TypeSpec> GetSomeVariedTypes()
    {
        yield return new NamedTypeSpec("Swift.Int");
        yield return new ClosureTypeSpec();
        yield return new ProtocolListTypeSpec(GetSomeNamedTypes());
    }

    [Fact]
    public static void TestMultiTuple()
    {
        var tuple = new TupleTypeSpec(GetSomeTypes());
        Assert.Equal(3, tuple.Elements.Count);
        var named = tuple.Elements[0] as NamedTypeSpec;
        Assert.NotNull(named);
        Assert.Equal("a", named.Name);
    }

    [Fact]
    public static void TestTupleToString()
    {
        var tuple = new TupleTypeSpec(GetSomeTypes());
        Assert.Equal("(a, b, c)", tuple.ToString());
    }

    [Fact]
    public static void TestProtocolListTypeSpec()
    {
        var proto = new ProtocolListTypeSpec(GetSomeNamedTypes());
        Assert.Equal(3, proto.Protocols.Count);
        Assert.Equal("a & b & c", proto.ToString());
    }

    [Fact]
    public static void TestEmptyClosure()
    {
        var clos = new ClosureTypeSpec(null, null);
        Assert.True(clos.Arguments.IsEmptyTuple);
        Assert.True(clos.ReturnType.IsEmptyTuple);
        Assert.Equal("() -> ()", clos.ToString());
    }
    
    [Fact]
    public static void TestOneOnOne()
    {
        var clos = new ClosureTypeSpec(new NamedTypeSpec("Swift.Int"), new NamedTypeSpec("Swift.Bool"));
        Assert.NotNull(clos.Arguments);
        Assert.NotNull(clos.ReturnType);
        Assert.True(clos.Arguments is NamedTypeSpec named && named.Name == "Swift.Int");
        Assert.True(clos.ReturnType is NamedTypeSpec named1 && named1.Name == "Swift.Bool");
        Assert.Equal("(Swift.Int) -> Swift.Bool", clos.ToString());
    }

    [Fact]
    public static void TestLargerClosure()
    {
        var tuple = new TupleTypeSpec(GetSomeTypes());
        var clos = new ClosureTypeSpec(tuple, new NamedTypeSpec("Swift.Int"));
        Assert.True(true);
        Assert.True(clos.Arguments is TupleTypeSpec tuple1 && tuple1.Elements.Count == 3);
    }

    [Fact]
    public static void TestWithAttribute()
    {
        var clos = new ClosureTypeSpec(new NamedTypeSpec("Swift.Int"), new NamedTypeSpec("Swift.Bool"));
        clos.Attributes.Add(new TypeSpecAttribute("escaping"));
        Assert.Single(clos.Attributes);
        Assert.True(clos.IsEscaping);
    }

    [Fact]
    public static void TestGenericEasy()
    {
        var ns = new NamedTypeSpec("Foo.Bar");
        ns.GenericParameters.Add(new NamedTypeSpec("Swift.Int"));
        Assert.Equal("Foo.Bar<Swift.Int>", ns.ToString());
    }

    [Fact]
    public static void TestGenericChallenging()
    {
        var ns = new NamedTypeSpec("Foo.Bar");
        ns.GenericParameters.Add(new TupleTypeSpec(GetSomeVariedTypes()));
        Assert.Equal("Foo.Bar<(Swift.Int, () -> (), a & b & c)>", ns.ToString());
    }

    [Fact]
    public static void TestProtocolListAlphabetical1 ()
    {
        var specs = new NamedTypeSpec [] {
            new NamedTypeSpec ("🤡Foo"),
            new NamedTypeSpec ("💩Foo"),
        };

        var protos = new ProtocolListTypeSpec (specs);
        Assert.Equal ("💩Foo & 🤡Foo", protos.ToString ());
    }

    [Fact]
    public static void TestProtocolListMatch ()
    {
        var specs1 = new NamedTypeSpec [] {
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Dfoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var specs2 = new NamedTypeSpec [] {
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Dfoo"),
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var protos1 = new ProtocolListTypeSpec (specs1);
        var protos2 = new ProtocolListTypeSpec (specs2);

        Assert.True (protos1.Equals (protos2));
    }

    [Fact]
    public static void TestProtocolListNotMatch ()
    {
        var specs1 = new NamedTypeSpec [] {
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Dfoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var specs2 = new NamedTypeSpec [] {
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Efoo"),
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var protos1 = new ProtocolListTypeSpec (specs1);
        var protos2 = new ProtocolListTypeSpec (specs2);

        Assert.False (protos1.Equals (protos2));
    }

    [Fact]
    public static void TestProtocolListNotMatchLength ()
    {
        var specs1 = new NamedTypeSpec [] {
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Dfoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var specs2 = new NamedTypeSpec [] {
            new NamedTypeSpec ("Afoo"),
            new NamedTypeSpec ("Dfoo"),
            new NamedTypeSpec ("Cfoo"),
            new NamedTypeSpec ("Efoo"),
            new NamedTypeSpec ("Bfoo")
        };

        var protos1 = new ProtocolListTypeSpec (specs1);
        var protos2 = new ProtocolListTypeSpec (specs2);

        Assert.False (protos1.Equals (protos2));
    }

}