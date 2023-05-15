Invoke-WebRequest -Uri https://github.com/wasmerio/wasmer/releases/download/v3.3.0/wasmer-windows-amd64.tar.gz -OutFile wasmer-windows-amd64.tar.gz

mkdir wasmer

tar -xzf wasmer-windows-amd64.tar.gz -C wasmer
