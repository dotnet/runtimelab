To generate bindings between .NET and Swift symbols such as metadata accessors, protocol conformance descriptors, or async function pointers are needed. 
However, these symbols are not part of the *abi.json* files we are using to get the majority of common symbols.

The [original proposal](https://github.com/dotnet/runtimelab/blob/feature/swift-bindings/src/docs/demangling.md#demangling) suggested reading *.dylib* files to retrieve all the necessary symbols.

The following is series of findings and proposals how to resolve this issue.

### Working with *.dylib*s for system frameworks is likely not an option

*.dylib* files for system frameworks are not publicly available:

> Big Sur introduces a dyld shared cache, where all of the system frameworks are built into a single optimized binary. The individual framework binaries are no longer present in the OS. 

> It turns out that all those libraries are instead prelinked together in a single big executable file referred to as dyld shared library cache. This file is then mapped in the address space of all executables running on the system, by the dynamic loader and linker (dyld).

The shared cache is located under */System/Volumes/Preboot/Cryptexes/OS/System/DriverKit/System/Library/dyld/dyld_shared_cache_arm64e* folder.
There does not seem to be any official on system tool (existing 3rd party ones) that would allow us to access this shared cache and reverse engineering it ourselves doesn't seem to be like a good idea.

> However, over time, Apple has been adding various optimizations specific to the DSC for how Objective-C metadata is encoded and retrieved — and that’s something reverse engineering tools must be constantly updated to support.

That said, some version of the *.dylib*s for system frameworks are present on the disk, e.g., *~/Library/Developer/Xcode/iOS DeviceSupport/iPhone12,1 17.5.1 (21F90)/Symbols/System/Library/Frameworks/StoreKit.framework/StoreKit*.
However, these dylib files don't carry all the necessary symbols which we would need (e.g., running `nm -a` on the *.dylib* doesn't show any protocol conformance descriptors).

Apple publishes a `dyld` tool (https://github.com/apple-oss-distributions/dyld) that could be used to get the information from shared cache but it is not part of the public API and using it would require building from source.


### Reading framework dylibs using `dyld_info` almost works
Apple provides `/usr/bin/dyld_info` tool which can be run to display information that dyld uses from binaries.
The tool can be run on paths to *.dylib*s that are in the dyld cache but not on disk.

For example: 
```
dyld_info -exports -platform /System/Library/Frameworks/StoreKit.framework/StoreKit 
```
returns the exports for macOS version of StoreKit
```
/System/Library/Frameworks/StoreKit.framework/StoreKit [arm64e]:
    -platform:
        platform     minOS      sdk
           macOS     15.3      15.3   
    -exports:
        offset      symbol
        0x00096F3C  _$s10Foundation14DateComponentsV8StoreKitE18subscriptionPeriodAcD7ProductV012SubscriptionG0V_tcfC
        0x00130E14  _$s10Foundation4DataV15StoreKit_SharedE16base64URLEncoded7optionsACSgx_So27NSDataBase64DecodingOptionsVtcSyRzlufC
        0x00121F70  _$s10Foundation4DataV8StoreKitEyACSgAD12BackingValueOcfC
        0x00121D44  _$s10Foundation4DateV8StoreKitEyACSgAD12BackingValueOcfC
        0x001221A0  _$s10Foundation4UUIDV8StoreKitEyACSgAD12BackingValueOcfC  
        ...
```

corresponding MacCatalyst exports can be retrieved by `dyld_info -exports -platform /System/iOSSupport/System/Library/Frameworks/StoreKit.framework/StoreKit`.

However, exported symbols might differ between platforms (e.g., some APIs are not exported on MacCatalyst but are on iOS or some APIs might be different), 
for example, `s7SwiftUI012GestureStateC0Vyxq_GAA09PrimitiveC0AAWP` is exported from iOS SwiftUI but is NOT exported from MacOS and MacCatalyst versions of the same framework.
Consequently, we need to retrieve the symbols for each platform we generate bindings for.

Unfortunately, `dyld_info` does not provide an option (TODO: need to confirm) to select the target platform.

### Parsing *.tbd* (text-based stub libraries) files for each platform (candidate solution)
*.tbd* files for all installed platforms are available in `Xcode` subdirectories.

For example, StoreKit *.tbd* files can be found under:
- MacCatalyst: */Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk/System/iOSSupport/System/Library/Frameworks/StoreKit.framework/StoreKit.tbd*
- macOS: */Applications/Xcode.app/Contents/Developer/Platforms/MacOSX.platform/Developer/SDKs/MacOSX.sdk/System/Library/Frameworks/StoreKit.framework/StoreKit.tbd*
- iOS: */Applications/Xcode.app/Contents/Developer/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk/System/Library/Frameworks/StoreKit.framework/StoreKit.tbd*

These *.tbd* files contain all the export symbols in the following format:
```
--- !tapi-tbd
tbd-version:     4
targets:         [ armv7-ios, armv7s-ios, arm64-ios, arm64e-ios ]
install-name:    '/System/Library/Frameworks/StoreKit.framework/StoreKit'
swift-abi-version: 7
exports:
  - targets:         [ armv7-ios, armv7s-ios, arm64-ios, arm64e-ios ]
    symbols:         [ '_$s10Foundation14DateComponentsV8StoreKitE18subscriptionPeriodAcD7ProductV012SubscriptionG0V_tcfC', 
                       '_$s10Foundation4DataV8StoreKitEyACSgAD12BackingValueOcfC', 
                       '_$s10Foundation4DateV8StoreKitEyACSgAD12BackingValueOcfC', 
                       '_$s10Foundation4UUIDV8StoreKitEyACSgAD12BackingValueOcfC', 
                       ...
```

New *.tbd* files can also be generated using the `tapi` tool provided by Apple.
To parse *.tbd* files we will have to implement a parser that would replace the [MachO](https://github.com/dotnet/runtimelab/blob/feature/swift-bindings/src/Swift.Bindings/src/Demangler/MachO.cs) in the projection tooling.

The *.tbd* come in two formats:
- YAML for *.tbd* format versions 1-4
- JSON for *.tbd* format versions 5+

--- 
Sources:
- https://www.nowsecure.com/blog/2024/09/11/reversing-ios-system-libraries-using-radare2-a-deep-dive-into-dyld-cache-part-1/
- https://developer.apple.com/forums/thread/659324
- https://keith.github.io/xcode-man-pages/dyld_info.1.html
