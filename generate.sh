#!/usr/bin/env bash
set -e
set -o pipefail

usage()
{
  echo "Common settings:"
  echo "  --platform <value>         Platform: iPhoneOS, iPhoneSimulator, AppleTVOS, AppleTVSimulator, MacOSX, MacCatalyst"
  echo "  --version <value>          Version of the SDK"
  echo "  --framework <value>        Framework to generate bindings for"
  echo "  --configuration <value>    Configuration: Debug, Release"
  echo "  --tool                     Use the NuGet tool instead of the local build"
  echo "  --experimental             Generates only Runtime.Swift namespace when bindings for frameworks are not complete"
  echo "  --help                     Print help and exit (short: -h)"
  echo ""
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
version=''
frameworks=()
configuration='Debug'
tool=false
experimental=false
dotnet_version="net9.0"

while [[ $# > 0 ]]; do
  opt="$(echo "${1/#--/-}" | tr "[:upper:]" "[:lower:]")"
  case "$opt" in
    -platform)
      platform=$2
      shift
      ;;
    -version)
      version=$2
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
    -tool)
      tool=true
      ;;
    -experimental)
      experimental=true
      ;;
    -help|-h)
      usage
      exit 0
      ;;
  esac

  shift
done

if [[ $platform != "iPhoneOS" && $platform != "iPhoneSimulator" && $platform != "AppleTVOS" && $platform != "AppleTVSimulator" && $platform != "MacOSX" && $platform != "MacCatalyst" ]]; then
    echo "Error: Invalid platform '$platform'."
    usage
    exit 1
fi

sdk_path="$(xcode-select -p)/Platforms/$platform.platform/Developer/SDKs/$platform.sdk"
fpath="$sdk_path/System/Library/Frameworks/"

if [[ -z $version ]]; then
    if [[ $platform == "MacCatalyst" ]]; then
        version=$(xcrun --sdk iphoneos --show-sdk-version)
    else
        version=$(xcrun --sdk "$(echo "$platform" | tr '[:upper:]' '[:lower:]')" --show-sdk-version)
    fi
fi

case "$platform" in
    "iPhoneOS")
        target="apple-ios"
        target_with_version="apple-ios$version"
        arch="arm64e"
        ;;
    "iPhoneSimulator")
        target="apple-ios-simulator"
        target_with_version="apple-ios$version-simulator"
        arch="x86_64"
        ;;
    "AppleTVOS")
        target="apple-tvos"
        target_with_version="apple-tvos$version"
        arch="arm64e"
        ;;
    "AppleTVSimulator")
        target="apple-tvos-simulator"
        target_with_version="apple-tvos$version-simulator"
        arch="x86_64"
        ;;
    "MacOSX")
        target="apple-macos"
        target_with_version="apple-macos$version"
        arch="arm64e"
        ;;
    "MacCatalyst")
        target="apple-ios-macabi"
        target_with_version="apple-ios$version-macabi"
        arch="arm64e"

        # Override the SDK path for MacCatalyst
        sdk_path="$(xcode-select -p)/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk"
        fpath="$sdk_path/System/iOSSupport/System/Library/Frameworks"
        ;;
    *)
        echo "Error: Unsupported platform '$platform'."
        exit 1
        ;;
esac


# Output directory for generated bindings
output_dir="./artifacts/$platform"

rm -rf "$output_dir"
mkdir -p "$output_dir"

cd "$output_dir"

# Function to extract ABI file
function ExtractABI {
    local framework=$1

    echo "Generating ABI for framework '$framework' with target '$arch-$target'"

    local swift_interface_path="$fpath/$framework.framework/Modules/$framework.swiftmodule/$arch-$target.swiftinterface"

    if [ ! -f "$swift_interface_path" ]; then
        echo "Error: Swift interface file not found for framework '$framework'."
        return 1
    fi

    xcrun swift-frontend -compile-module-from-interface "$swift_interface_path" \
        -target "$arch-$target_with_version" \
        -module-name "$framework" \
        -sdk "$sdk_path" \
        -Fsystem "$fpath" \
        -emit-abi-descriptor-path "./$framework.abi.json"
}

# Function to generate bindings
function InvokeProjectionTooling {
    local framework=$1

    if $tool; then
        echo "Using tool to generate bindings for framework '$framework'"
        local script_path="swiftbindings"
    else
        echo "Using local build to generate bindings for framework '$framework'"
        local script_path="$scriptroot/artifacts/bin/Swift.Bindings/$configuration/$dotnet_version/Swift.Bindings.dll"
    fi

    $scriptroot/dotnet.sh $script_path -a "./$framework.abi.json" -d "/System/Library/Frameworks/$framework.framework/$framework" -t "$fpath/$framework.framework/$framework.tbd" -o "./"

    # Patch library name in generated C# code for async methods
    local frameworkPath="/System/Library/Frameworks/$framework.framework/$framework"
    sed -i '' "/_async/ s|$frameworkPath|SwiftBindings.framework/SwiftBindings|g" "./Swift.$framework.cs"

    echo ""
    echo "C# source code for Swift.$framework.cs:"
    cat "./Swift.$framework.cs"
    echo ""

    if $experimental; then
        rm -rf "./Swift.$framework.cs"
        rm -rf "./Swift.$framework.swift"
    fi

    if [ -f "Swift.$framework.swift" ]; then
        echo "import $framework" | cat - Swift.$framework.swift > temp && mv temp Swift.$framework.swift
    fi
}

# Function to create a Swift xcframework
function CreateFramework {
    local framework="SwiftBindings"

    if ! ls *.swift 1> /dev/null 2>&1; then
        echo "No Swift files found to build. Skipping..."
        return
    fi

    # x86_64 (simulators, macOS, and maccatalyst)
    if [[ $platform == "iPhoneSimulator" || $platform == "AppleTVSimulator" || $platform == "MacOSX" || $platform == "MacCatalyst" ]]; then
    echo "Building $framework for $platform x86_64..."
    xcrun --sdk $sdk_path swiftc -emit-library -target x86_64-$target_with_version -module-name $framework -o $framework-$platform-x86_64.dylib *.swift -F $fpath -sdk $sdk_path -Xlinker -install_name -Xlinker @rpath/$framework.framework/$framework
    fi

    # arm64
    echo "Building $framework for $platform arm64..."
    xcrun --sdk $sdk_path swiftc -emit-library -target arm64-$target_with_version -module-name $framework -o $framework-$platform-arm64.dylib *.swift -F $fpath -sdk $sdk_path -Xlinker -install_name -Xlinker @rpath/$framework.framework/$framework

    # Function to create a Swift framework
    create_framework() {
        variant=$1
        dylib="$framework-$variant.dylib"
        framework_dir="$variant/$framework.framework"

        echo "Creating framework bundle for $variant..."
        rm -rf "$framework_dir"
        mkdir -p "$framework_dir/Versions/A"

        cp "$dylib" "$framework_dir/Versions/A/$framework"
        ln -s "Versions/A/$framework" "$framework_dir/$framework"

        cat > "$framework_dir/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.yourcompany.$framework</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
</dict>
</plist>
EOF

        mkdir -p $framework_dir/Modules
        cat > $framework_dir/Modules/module.modulemap <<EOF
framework module MySwiftFramework {
    umbrella header "MySwiftFramework.h"
    export *
    module * { export * }
}
EOF

        echo "// Header file" > $framework_dir/$framework.h
    }

    # x86_64 (simulators, macOS, and maccatalyst)
    if [[ $platform == "iPhoneSimulator" || $platform == "AppleTVSimulator" || $platform == "MacOSX" || $platform == "MacCatalyst" ]]; then
        create_framework "$platform-x86_64"
        local args_x64=(-framework "$platform-x86_64/$framework.framework")
        args_x64+=(-output "$framework-x86_64.xcframework")
        xcodebuild -create-xcframework "${args_x64[@]}"

        # zip the xcframework
        echo "Zipping xcframework..."
        zip -r "$framework-x86_64.xcframework.zip" "$framework-x86_64.xcframework"
    fi

    # arm64
    create_framework "$platform-arm64"
    local args_arm64=(-framework "$platform-arm64/$framework.framework")
    args_arm64+=(-output "$framework-arm64.xcframework")
    xcodebuild -create-xcframework "${args_arm64[@]}"

    # zip the xcframework
    echo "Zipping xcframework..."
    zip -r "$framework-arm64.xcframework.zip" "$framework-arm64.xcframework"
}

# Function to generate project
function CreateProject {
    local project_file="./Swift.Bindings.$platform.Experimental.csproj"

    cat <<EOL > "$project_file"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$dotnet_version</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <IsPackable>true</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <ItemGroup>
    <!-- iOS is arm64 only -->
    <Content Include="./SwiftBindings-arm64.xcframework.zip" Condition="Exists('./SwiftBindings-arm64.xcframework.zip')">
        <PackagePath>runtimes/ios-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-arm64.xcframework.zip" Condition="Exists('./SwiftBindings-arm64.xcframework.zip')">
        <PackagePath>runtimes/iossimulator-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-x86_64.xcframework.zip" Condition="Exists('./SwiftBindings-x86_64.xcframework.zip')">
        <PackagePath>runtimes/iossimulator-x64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <!-- tvOS is arm64 only -->
    <Content Include="./SwiftBindings-arm64.xcframework.zip" Condition="Exists('./SwiftBindings-arm64.xcframework.zip')">
        <PackagePath>runtimes/tvos-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-arm64.xcframework.zip" Condition="Exists('./SwiftBindings-arm64.xcframework.zip')">
        <PackagePath>runtimes/tvossimulator-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-x86_64.xcframework.zip" Condition="Exists('./SwiftBindings-x86_64.xcframework.zip')">
        <PackagePath>runtimes/tvossimulator-x64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-arm64.xcframework.zip" Condition="Exists('./SwiftBindings-arm64.xcframework.zip')">
        <PackagePath>runtimes/osx-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-x86_64.xcframework.zip" Condition="Exists('./SwiftBindings-x86_64.xcframework.zip')">
        <PackagePath>runtimes/osx-x64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-arm64.xcframework.zip" Condition="Exists('./SwiftBindings-arm64.xcframework.zip')">
        <PackagePath>runtimes/maccatalyst-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings-x86_64.xcframework.zip" Condition="Exists('./SwiftBindings-x86_64.xcframework.zip')">
        <PackagePath>runtimes/maccatalyst-x64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
  </ItemGroup>
</Project>
EOL

    $scriptroot/dotnet.sh build "$project_file"
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

    CreateFramework
    CreateProject

    echo "Process completed."
}

Generate
