using Xamarin;

public static class MachoExtensions
{
    /// <summary>
    /// Get an enumerable of MachOFile which matches the given Abi
    /// </summary>
    /// <param name="files">A set of MachOFile to search</param>
    /// <param name="abi">The abi to match</param>
    /// <returns>a (possibly empty) enumeration of MachOFile each of which matches the Abi</returns>
    public static IEnumerable<MachOFile> WithAbi(this IEnumerable<MachOFile> files, Abi abi)
    {
        return files.Where(file => MachO.GetArch(file.cputype, file.cpusubtype) == abi);
    }

    /// <summary>
    /// Get all the public symbols from a collection MachOFile matching a particular Abi
    /// </summary>
    /// <param name="allFiles">unfiltered files</param>
    /// <param name="abi">the abi to match</param>
    /// <returns>all public symbols in the set of MachOFiles that matches the Abi</returns>
    public static IEnumerable<NListEntry> PublicSymbols(this IEnumerable<MachOFile> allFiles, Abi abi)
    {
        var files = allFiles.WithAbi(abi);
        foreach (var file in files) {
            foreach (var symbols in file.load_commands.OfType<SymTabLoadCommand>()) {
                foreach(var publicSym in symbols.PublicSymbols) {
                    yield return publicSym;
                }
            }
        }
    }
}