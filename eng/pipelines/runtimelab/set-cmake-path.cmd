# Save the current PATH because init-vs-env.cmd will add enough to the PATH that calling it twice
# will exceed the command length limit.
set OLD_PATH=%PATH%
call "%RepoRoot%eng\native\init-vs-env.cmd" wasm || exit /b 1

call set CMakeDir=%%CMakePath:\cmake.exe=%%

echo CMakeDir is %CMakeDir%
echo Setting PATH to %OLD_PATH%;%CMakeDir%
echo ##vso[task.setvariable variable=PATH]%OLD_PATH%;%CMakeDir%
