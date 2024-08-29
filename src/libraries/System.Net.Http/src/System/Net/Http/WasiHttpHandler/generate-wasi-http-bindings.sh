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
curl -OL https://github.com/WebAssembly/wasi-http/archive/refs/tags/v0.2.1.tar.gz
tar xzf v0.2.1.tar.gz
cat >wasi-http-0.2.1/wit/world.wit <<EOF
world wasi-http {
  import outgoing-handler;
}
EOF
wit-bindgen c-sharp -w wasi-http -r native-aot --internal wasi-http-0.2.1/wit
