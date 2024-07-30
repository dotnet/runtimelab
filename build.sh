#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

function is_cygwin_or_mingw()
{
  case $(uname -s) in
    CYGWIN*)    return 0;;
    MINGW*)     return 0;;
    *)          return 1;;
  esac
}

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done

scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

if is_cygwin_or_mingw; then
  # if bash shell running on Windows (not WSL),
  # pass control to powershell build script.
  scriptroot=$(cygpath -d "$scriptroot")
  powershell -c "$scriptroot\\build.cmd" $@
else
  if [[ "$*" == *"wasm"* && "$*" == *"-ci"* ]]; then
    # This is a bit of a workaround for the fact that the pipelines do not have a great
    # way of preserving the environment between scripts. Set by install-emscripten.ps1.
    if [[ -n $NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH ]]; then
        source $NATIVEAOT_CI_WASM_BUILD_EMSDK_PATH/emsdk_env.sh
    fi
  fi
  "$scriptroot/eng/build.sh" $@
fi
