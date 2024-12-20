#!/usr/bin/env bash
set -e
set -o pipefail

usage()
{
  echo "Common settings:"
  echo "  --platform <value>         Platform: MacOSX, iPhoneOS, iPhoneSimulator, AppleTVOS, AppleTVSimulator"
  echo "  --arch <value>             Architecture: arm64e-apple-macos, x86_64-apple-macos"
  echo "  --framework <value>        Framework to generate bindings for"
  echo "  --configuration <value>    Configuration: Debug, Release"
  echo "  --help                     Print help and exit (short: -h)"
  echo ""

  echo "Actions:"
  echo "  --experimental             Generates only Runtime.Swift namespace when bindings for frameworks are not complete"
}

source="${BASH_SOURCE[0]}"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done

scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

platform=''
arch=''
frameworks=()
configuration='Debug'
experimental=false

output_dir="./GeneratedBindings"

while [[ $# > 0 ]]; do
  opt="$(echo "${1/#--/-}" | tr "[:upper:]" "[:lower:]")"
  case "$opt" in
    -help|-h)
      usage
      exit 0
      ;;
    -experimental)
      experimental=true
      ;;
    -platform)
      platform=$2
      shift
      ;;
    -arch)
      arch=$2
      shift
      ;;
    -framework)
      frameworks+=("$2")
      shift
      ;;
    -configuration)
      configuration=$2
      shift
      ;;
  esac

  shift
done

# Output directory for generated bindings
rm -rf "$output_dir"
mkdir -p "$output_dir"

cd "$output_dir"

# Function to extract ABI file
function ExtractABI {
    local framework=$1

    echo "Generating ABI for framework '$framework', platform '$platform', architecture '$arch'"
    
    local sdk_path=$(xcrun -sdk $(echo "$platform" | tr '[:upper:]' '[:lower:]') --show-sdk-path)
    local swift_interface_path="$(xcode-select -p)/Platforms/${platform}.platform/Developer/SDKs/${platform}.sdk/System/Library/Frameworks/${framework}.framework/Versions/Current/Modules/${framework}.swiftmodule/${arch}.swiftinterface"

    if [ ! -f "$swift_interface_path" ]; then
        echo "Error: Swift interface file not found for framework '$framework'."
        return 1
    fi

    xcrun swift-frontend -compile-module-from-interface "$swift_interface_path" \
        -module-name "$framework" \
        -sdk "$sdk_path" \
        -emit-abi-descriptor-path "./$framework.abi.json"
}

# Function to generate bindings
function InvokeProjectionTooling {
    local framework=$1

    $scriptroot/dotnet.sh $scriptroot/artifacts/bin/Swift.Bindings/$configuration/net9.0/Swift.Bindings.dll -a "./$framework.abi.json" -d "/System/Library/Frameworks/$framework.framework/$framework" -o "./"

    echo ""
    echo "C# source code for Swift.$framework.cs:"
    cat "./Swift.$framework.cs"
    echo ""

    if $experimental; then
        rm -rf "./Swift.$framework.cs"
    fi
}

# Function to generate NuGet package
function PackNuGet {
    local project_file="./Swift.Bindings.${platform}.Experimental.csproj"

    cat <<EOL > "$project_file"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <IsPackable>true</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
</Project>
EOL

    $scriptroot/dotnet.sh pack "$project_file"
}

function Generate {
    for framework in "${frameworks[@]}"; do
        echo "Processing framework: $framework"

        if ExtractABI "$framework"; then
            echo "Generating bindings for framework '$framework'"
            InvokeProjectionTooling "$framework"
        else
            echo "Skipping framework '$framework' due to errors."
        fi
    done

    PackNuGet

    echo "Process completed."
}

Generate
