#!/usr/bin/env bash
set -e

echo Setting EMSDK_PYTHON to /usr/bin/python3
echo '##vso[task.setvariable variable=EMSDK_PYTHON]'/usr/bin/python3
echo

echo Installing LLDB, QEMU
sudo tdnf install -y lldb python3-lldb qemu-user
echo
