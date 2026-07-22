// Tiny helper: resolve an RVA within coreclr.dll to a symbol name using
// the public Microsoft symbol server. Not part of the GC; debug tool only.
#include <windows.h>
#include <dbghelp.h>
#include <cstdio>
#include <cstdlib>

#pragma comment(lib, "dbghelp.lib")

static BOOL CALLBACK SymCallback(HANDLE hProcess, ULONG ActionCode, ULONG64 CallbackData, ULONG64 UserContext)
{
    if (ActionCode == CBA_DEBUG_INFO)
    {
        printf("%s", (const char*)CallbackData);
    }
    return FALSE;
}

int main(int argc, char** argv)
{
    if (argc < 3)
    {
        printf("usage: symresolve <dllpath> <rva-hex>\n");
        return 1;
    }
    const char* dllPath = argv[1];
    DWORD64 rva = strtoull(argv[2], nullptr, 16);

    HMODULE hDbgHelp = GetModuleHandleA("dbghelp.dll");
    char dbghelpPath[MAX_PATH] = {};
    if (hDbgHelp) GetModuleFileNameA(hDbgHelp, dbghelpPath, MAX_PATH);
    printf("dbghelp.dll loaded from: %s\n", dbghelpPath);

    HANDLE hProc = GetCurrentProcess();
    SymSetOptions(SYMOPT_UNDNAME | SYMOPT_LOAD_LINES | SYMOPT_DEBUG);
    if (!SymInitialize(hProc, "SRV*C:\\temp\\symbols*https://msdl.microsoft.com/download/symbols", FALSE))
    {
        printf("SymInitialize failed: %lu\n", GetLastError());
        return 1;
    }
    SymRegisterCallback64(hProc, SymCallback, 0);

    DWORD64 base = SymLoadModuleEx(hProc, nullptr, dllPath, nullptr, 0x10000000, 0, nullptr, 0);
    if (base == 0)
    {
        printf("SymLoadModuleEx failed: %lu\n", GetLastError());
        return 1;
    }
    printf("Module base: 0x%llx\n", base);

    IMAGEHLP_MODULE64 modInfo;
    modInfo.SizeOfStruct = sizeof(modInfo);
    if (SymGetModuleInfo64(hProc, base, &modInfo))
    {
        printf("SymType=%d LoadedPdbName=%s PdbSig70 available, CVSig=%s\n", modInfo.SymType, modInfo.LoadedPdbName, modInfo.CVData);
    }
    else
    {
        printf("SymGetModuleInfo64 failed: %lu\n", GetLastError());
    }

    DWORD64 addr = base + rva;

    char buf[sizeof(SYMBOL_INFO) + MAX_SYM_NAME];
    SYMBOL_INFO* sym = (SYMBOL_INFO*)buf;
    sym->SizeOfStruct = sizeof(SYMBOL_INFO);
    sym->MaxNameLen = MAX_SYM_NAME;

    DWORD64 disp = 0;
    if (SymFromAddr(hProc, addr, &disp, sym))
    {
        printf("Symbol: %s + 0x%llx\n", sym->Name, disp);
    }
    else
    {
        printf("SymFromAddr failed: %lu\n", GetLastError());
    }

    IMAGEHLP_LINE64 line;
    line.SizeOfStruct = sizeof(line);
    DWORD lineDisp = 0;
    if (SymGetLineFromAddr64(hProc, addr, &lineDisp, &line))
    {
        printf("Line: %s:%lu\n", line.FileName, line.LineNumber);
    }
    else
    {
        printf("SymGetLineFromAddr64 failed: %lu\n", GetLastError());
    }

    SymCleanup(hProc);
    return 0;
}
