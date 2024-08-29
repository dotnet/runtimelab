#!/bin/sh

set -ex

# This script will regenerate the `wit-bindgen`-generated files in this
# directory.

# Prerequisites:
#   POSIX shell
#   tar
#   [cargo](https://rustup.rs/)
#   [curl](https://curl.se/download.html)

# TODO: switch to crates.io release once https://github.com/bytecodealliance/wit-bindgen/pull/1040 is merged and released
cargo install --locked --no-default-features --features csharp --git https://github.com/dicej/wit-bindgen --rev 694fd927 wit-bindgen-cli
wit-bindgen c-sharp -w library -r native-aot wit
rm LibraryWorld_wasm_import_linkage_attribute.cs
