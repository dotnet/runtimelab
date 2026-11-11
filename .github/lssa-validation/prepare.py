import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root, project = map(Path, sys.argv[1:])
feed = root / "artifacts/packages/Debug/Shipping"
pattern = re.compile(r"microsoft\.dotnet\.ilcompiler\.llvm\.(\d+\.\d+\.\d+[^/]*)\.nupkg", re.I)
versions = [
    match[1]
    for package in feed.glob("*.nupkg")
    if not package.name.lower().endswith(".symbols.nupkg")
    and (match := pattern.fullmatch(package.name))
]
if len(versions) != 1:
    sys.exit(f"Expected one LLVM ILC package in {feed}, found {versions}")
version = versions[0]
framework = ".".join(version.split(".")[:2])

config = ET.parse(root / "NuGet.config")
config_root = config.getroot()
mapping = config_root.find("packageSourceMapping")
if mapping is not None:
    config_root.remove(mapping)
sources = config_root.find("packageSources")
ET.SubElement(sources, "add", key="ci-compiler", value=str(feed))
config.write(project / "NuGet.Config", encoding="utf-8")

(project / "LssaRepro.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net{framework}</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Optimize>true</Optimize>
    <PublishTrimmed>true</PublishTrimmed>
    <SelfContained>true</SelfContained>
    <UseAppHost>false</UseAppHost>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="[{version}]" />
    <PackageReference Include="runtime.linux-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="[{version}]" />
  </ItemGroup>
</Project>
""", encoding="utf-8")

(project / "Program.cs").write_text("""using System;
using System.Runtime.CompilerServices;

static class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(Problem(args.Length == 0 ? null : args[0], args));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe object Problem(object x, object y)
    {
        object z = x != null ? y : x;
        Use(&z);
        return z;
    }

    public static unsafe void Use<T>(T* arg) { }
}
""", encoding="utf-8")
