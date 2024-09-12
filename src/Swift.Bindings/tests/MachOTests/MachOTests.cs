using System.Security.Cryptography.X509Certificates;
using Xamarin;
using Xunit;

namespace BindingsGeneration.Tests
{
    public class MachOTests : IClassFixture<MachOTests.TestFixture>
    {
        private readonly TestFixture _fixture;
        private static string _dylibPath;

        public MachOTests(TestFixture fixture)
        {
            _fixture = fixture;
        }

        public class TestFixture
        {
            static TestFixture()
            {
                InitializeResources();
            }

            private static void InitializeResources()
            {
                var (_, dylibPath) = BindingsGenerator.GetResolvedPathAndDylibPath("FrozenStructs/FrozenStructsTests.abi.json", "FrozenStructs/");
                _dylibPath = dylibPath;
            }

            [Fact]
            public static void AssureDylibExists()
            {
                Assert.False(String.IsNullOrEmpty(_dylibPath));
                Assert.True(File.Exists(_dylibPath));
            }
            
            [Fact]
            public static void HasAbis()
            {
                var abis = MachO.GetArchitectures(_dylibPath);
                Assert.NotEmpty(abis);
            }

            [Fact]
            public static void IsMachOFile()
            {
                Assert.True(MachO.IsMachOFile(_dylibPath));
            }

            [Fact]
            public static void IsCorrectMinOS()
            {
                var macho = MachO.Read(_dylibPath).FirstOrDefault();
                Assert.NotNull(macho);
                var minOS = macho.load_commands.OfType<MinCommand>().FirstOrDefault();
                // this library is build without a minOS load command
                Assert.Null(minOS);
                var bv = macho.load_commands.OfType<BuildVersionCommand>().FirstOrDefault();
                Assert.NotNull(bv);
                Assert.Equal(new Version(11, 0, 0), bv.MinOS);
                Assert.Equal(new Version(13, 3, 0), bv.Sdk);
                Assert.Equal(MachO.Platform.MacOS, bv.Platform);
            }

            [Fact]
            public static void HasSymbols()
            {
                var abis = MachO.GetArchitectures(_dylibPath);
                var symbols = MachO.Read(_dylibPath).PublicSymbols(abis[0]);
                Assert.NotEmpty(symbols);
                // this depends on the contents of the library. If you changed
                // the contents of FrozenStructs, then this will likely fail.
                Assert.Equal(522, symbols.Count());
            }

            [Fact]
            public static void NoPrivate()
            {
                var abis = MachO.GetArchitectures(_dylibPath);
                var symbols = MachO.Read(_dylibPath).PublicSymbols(abis[0]);
                var any = symbols.Any(sy => sy.IsPrivate);
                Assert.False(any);
            }
        }
    }
}