using BindingsGeneration.Demangling;
using Xunit;

namespace BindingsGeneration.Tests;

public class BasicDemanglingTests : IClassFixture<BasicDemanglingTests.TestFixture>
{
    private readonly TestFixture _fixture;

    public BasicDemanglingTests(TestFixture fixture)
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
    public void TestProtocolWitnessTable()
    {
        var symbol = "_$s20GenericTestFramework6ThingyCAA7StanleyAAWP";
        var demangler = new Swift5Demangler (symbol);
        var result = demangler.Run ();
        var protoWitnessReduction = result as ProtocolWitnessTableReduction;
        Assert.NotNull(protoWitnessReduction);
        Assert.Equal("GenericTestFramework.Thingy", protoWitnessReduction.ImplementingType.Name);
        Assert.Equal("GenericTestFramework.Stanley", protoWitnessReduction.ProtocolType.Name);
    }

    [Fact]
    public void TestOtherProtocolWitnessTable()
    {
        var symbol = "_$s20GenericTestFramework6ThingyCAA8IsItRealAAWP";
        var demangler = new Swift5Demangler (symbol);
        var result = demangler.Run ();
        var protoWitnessReduction = result as ProtocolWitnessTableReduction;
        Assert.NotNull(protoWitnessReduction);
        Assert.Equal("GenericTestFramework.Thingy", protoWitnessReduction.ImplementingType.Name);
        Assert.Equal("GenericTestFramework.IsItReal", protoWitnessReduction.ProtocolType.Name);
    }

    [Fact]
    public void TestFailDemangleNonsense()
    {
        var symbol = "_$ThisIsJustGarbage";
        var demangler = new Swift5Demangler (symbol);
        var result = demangler.Run ();
        var err = result as ReductionError;
        Assert.NotNull(err);
        Assert.Equal("No rule for node FirstElementMarker", err.Message);
    }

    [Fact]
    public void TestFailMetadataAccessor()
    {
        var symbol = "_$s20GenericTestFramework6ThingyCMa";
        var demangler = new Swift5Demangler (symbol);
        var result = demangler.Run ();
        var err = result as ReductionError;
        Assert.NotNull(err);
        Assert.Equal("No rule for node TypeMetadataAccessFunction", err.Message);
    }

    [Fact]
    public void TestNestedProtocolWitnessTable()
    {
        var symbol = "_$s20GenericTestFramework3FooC6ThingyCAA8IsItRealAAWP";
        var demangler = new Swift5Demangler (symbol);
        var result = demangler.Run ();
        var protoWitnessReduction = result as ProtocolWitnessTableReduction;
        Assert.NotNull(protoWitnessReduction);
        Assert.Equal("GenericTestFramework.Foo.Thingy", protoWitnessReduction.ImplementingType.Name);
        Assert.Equal("GenericTestFramework.IsItReal", protoWitnessReduction.ProtocolType.Name);
    }

    [Fact]
    public void TestOtherProtocolConformanceDescriptor()
    {
        var symbol = "_$s10someclient14CSAgeableProxyCAA7AgeableAAMc";
        var demangler = new Swift5Demangler (symbol);
        var result = demangler.Run ();
        var conf = result as ProtocolConformanceDescriptorReduction;
        Assert.NotNull(conf);
        Assert.Equal("someclient.CSAgeableProxy", conf.ImplementingType.Name);
        Assert.Equal("someclient.Ageable", conf.ProtocolType.Name);
        Assert.Equal("someclient", conf.Module);
    }
}