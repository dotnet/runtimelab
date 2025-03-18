#!/usr/bin/env bash
set -e
set -o pipefail

usage()
{
  echo "Common settings:"
  echo "  --platform <value>         Platform: MacOSX, iPhoneOS, iPhoneSimulator, AppleTVOS, AppleTVSimulator"
  echo "  --version <value>          Version of the SDK"
  echo "  --arch <value>             Architecture: arm64e-apple-macos, x86_64-apple-macos"
  echo "  --framework <value>        Framework to generate bindings for"
  echo "  --configuration <value>    Configuration: Debug, Release"
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
    -version)
      version=$2
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

    # Patch library name in generated C# code for async methods
    local frameworkPath="/System/Library/Frameworks/${framework}.framework/${framework}"
    sed -i '' "/_async/ s|${frameworkPath}|SwiftBindings|g" "./Swift.$framework.cs"

    echo ""
    echo "C# source code for Swift.$framework.cs:"
    cat "./Swift.$framework.cs"
    echo ""

    if $experimental; then
        rm -rf "./Swift.$framework.cs"
        rm -rf "./Swift.$framework.swift"
    fi

    if [ -f "Swift.${framework}.swift" ]; then
        echo "import ${framework}" | cat - Swift.${framework}.swift > temp && mv temp Swift.${framework}.swift
    fi
}

# Function to create a Swift xcframework
function CreateFramework {
    framework="SwiftBindings"

    if ! ls *.swift 1> /dev/null 2>&1; then
        echo "No Swift files found to build. Skipping..."
        return
    fi

    # x86_64
    echo "Building ${framework} for ${platform} x86_64..."
    swiftc -emit-library -target x86_64-apple-$(echo "$platform" | tr '[:upper:]' '[:lower:]')${version} -module-name ${framework} -o ${framework}-${platform}-x64.dylib *.swift -F /Applications/Xcode.app/Contents/Developer/Platforms/${platform}.platform/Developer/SDKs/${platform}.sdk/System/Library/Frameworks/

    # arm64
    echo "Building ${framework} for ${platform} arm64..."
    swiftc -emit-library -target arm64-apple-$(echo "$platform" | tr '[:upper:]' '[:lower:]')${version} -module-name ${framework} -o ${framework}-${platform}-arm64.dylib *.swift -F /Applications/Xcode.app/Contents/Developer/Platforms/${platform}.platform/Developer/SDKs/${platform}.sdk/System/Library/Frameworks/

    if [[ $platform == "MacOSX" ]]; then
        # MacCatalyst x86_64
        echo "Building ${framework} for MacCatalyst x86_64..."
        swiftc -emit-library -target x86_64-apple-ios18.1-macabi -module-name ${framework} -o ${framework}-maccatalyst-x64.dylib *.swift -F /Applications/Xcode.app/Contents/Developer/Platforms/${platform}.platform/Developer/SDKs/${platform}.sdk/System/iOSSupport/System/Library/Frameworks

        # MacCatalyst arm64
        echo "Building ${framework} for MacCatalyst arm64..."
        swiftc -emit-library -target arm64-apple-ios18.1-macabi -module-name ${framework} -o ${framework}-maccatalyst-arm64.dylib *.swift -F /Applications/Xcode.app/Contents/Developer/Platforms/${platform}.platform/Developer/SDKs/${platform}.sdk/System/iOSSupport/System/Library/Frameworks
    fi

    # Function to create a Swift framework
    create_framework() {
        variant=$1
        dylib="${framework}-${variant}.dylib"
        framework_dir="${variant}/${framework}.framework"

        echo "Creating framework bundle for ${variant}..."
        rm -rf "${framework_dir}"
        mkdir -p "${framework_dir}/Versions/A"

        cp "${dylib}" "${framework_dir}/Versions/A/${framework}"
        ln -s "Versions/A/${framework}" "${framework_dir}/${framework}"

        cat > "${framework_dir}/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.yourcompany.${framework}</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
</dict>
</plist>
EOF

        mkdir -p ${framework_dir}/Modules
        cat > ${framework_dir}/Modules/module.modulemap <<EOF
framework module MySwiftFramework {
    umbrella header "MySwiftFramework.h"
    export *
    module * { export * }
}
EOF

        echo "// Header file" > ${framework_dir}/${framework}.h
    }

    create_framework "$platform-x64"
    create_framework "$platform-arm64"
    args_x64=(-framework "$platform-x64/${framework}.framework")
    args_arm64=(-framework "$platform-x64/${framework}.framework")
    if [[ $platform == "MacOSX" ]]; then
        create_framework "maccatalyst-x64"
        create_framework "maccatalyst-arm64"
        args_x64+=(-framework "maccatalyst-x64/${framework}.framework")
        args_arm64+=(-framework "maccatalyst-arm64/${framework}.framework")
    fi

    args_x64+=(-output "${framework}_x64.xcframework")
    args_arm64+=(-output "${framework}_arm64.xcframework")
    xcodebuild -create-xcframework "${args_x64[@]}"
    xcodebuild -create-xcframework "${args_arm64[@]}"
}

# Function to generate project
function CreateProject {
    local project_file="./Swift.Bindings.Experimental.csproj"

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
  <ItemGroup>
    <Content Include="./SwiftBindings_arm64.xcframework/macos-arm64/**">
        <PackagePath>runtimes/osx-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings_x64.xcframework/macos-64/**">
        <PackagePath>runtimes/osx-x64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings_arm64.xcframework/ios-arm64-maccatalyst/**">
        <PackagePath>runtimes/maccatalyst-arm64/native/</PackagePath>
        <Pack>true</Pack>
    </Content>
    <Content Include="./SwiftBindings_x64.xcframework/ios-x64-maccatalyst/**">
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
