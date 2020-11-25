if ( -not (Test-Path -LiteralPath $Env:__versionSourceFile))
{
    <# Generate the dummy version.c and runtime_version.h, but only if they didn't exist to make sure we don't trigger unnecessary rebuild #>
    $__versionSourceLine = 'static char sccsid[] __attribute__((used)) = "@(#)No version information produced";'

    $Header= @"
#define RuntimeAssemblyMajorVersion 0
#define RuntimeAssemblyMinorVersion 0
#define RuntimeFileMajorVersion 0
#define RuntimeFileMinorVersion 0
#define RuntimeFileBuildVersion 0
#define RuntimeFileRevisionVersion 0
#define RuntimeProductMajorVersion 0
#define RuntimeProductMinorVersion 0
#define RuntimeProductPatchVersion 0
#define RuntimeProductVersion 0
"@

    $Header | Set-Content -Path $Env:runtimeVersionHeaderFile.Replace("`"","") -Encoding utf8
    $__versionSourceLine | Set-Content -Path $Env:__versionSourceFile.Replace("`"","") -Encoding utf8
}
