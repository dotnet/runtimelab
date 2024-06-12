$RootPath = $Args[0]

$NpmExePath = $Env:NPM_EXECUTABLE

Set-Location -Path $RootPath

& $NpmExePath install @bytecodealliance/jco
& $NpmExePath install @bytecodealliance/preview2-shim

