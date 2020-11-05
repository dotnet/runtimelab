// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//On unix make sure to compile using -ldl flag.

//Set this value accordingly to your workspace settings
#if defined(_WIN32)
#define PathToLibrary "bin\\Debug\\net5.0\\win-x64\\native\\NativeLibrary.dll"
#elif defined(__APPLE__)
#define PathToLibrary "./bin/Debug/net5.0/osx-x64/native/NativeLibrary.dylib"
#else
#define PathToLibrary "./bin/Debug/net5.0/linux-x64/native/NativeLibrary.so"
#endif

#ifdef _WIN32
#include "windows.h"
#define symLoad GetProcAddress
#else
#include "dlfcn.h"
#include <unistd.h>
#define symLoad dlsym
#endif

#include <stdlib.h>
#include <stdio.h>

#ifndef F_OK
#define F_OK 0
#endif

int callSumFunc(int a, int b);
char *callSumStringFunc(char *a, char *b);
char *callMergeStrArray(char **array, int count);
void *callPopulateStruct(void*);

static void** handle;

int main()
{
    #ifdef _WIN32
        HINSTANCE hdl = LoadLibraryA(PathToLibrary);
    #else
        void *hdl = dlopen(PathToLibrary, RTLD_LAZY);
    #endif
    handle = &hdl;

    // Check if the library file exists
    if (access(PathToLibrary, F_OK) == -1)
    {
        puts("Couldn't find library at the specified path");
        return 0;
    }

    // Sum two integers
    int sum = callSumFunc(2, 8);
    printf("The sum is %d \n\n", sum);

    // Concatenate two strings
    char *sumstring = callSumStringFunc("ok", "ko");
    printf("The concatenated string is %s \n\n", sumstring);

    // Concatenate x strings stored in an array
    char *strArray[4] = {"tragedy", "of", "the", "wise"};
    char *mergedString = callMergeStrArray(strArray, 4);
    printf("The merged string is:\n%s\n\n", mergedString);

    // Populate a struct
    typedef struct myStruct
    {
        char* name;
        int value;
    } t_myStruct;
    t_myStruct* newStruct = malloc(sizeof(t_myStruct));
    callPopulateStruct(newStruct);
    puts("---Struct Data---");
    printf("struct.name:\n%s\n", newStruct->name);
    printf("struct.value:\n%d\n", newStruct->value);

    // Free strings
    free(sumstring);
    free(mergedString);
}

int callSumFunc(int firstInt, int secondInt)
{
    const char* funcName = "add";
    typedef int (*myFunc)();
    myFunc MyImport = (myFunc)symLoad(*handle, funcName);

    int result = MyImport(firstInt, secondInt);

    // CoreRT libraries do not support unloading
    // See https://github.com/dotnet/corert/issues/7887
    return result;
}

char *callSumStringFunc(char *firstString, char *secondString)
{
    const char* funcName = "sumstring";

    // Declare a typedef
    typedef char *(*myFunc)();

    // Import Symbol named funcName
    myFunc MyImport = (myFunc)symLoad(*handle, funcName);

    // The C# function will return a pointer
    char *result = MyImport(firstString, secondString);

    // CoreRT libraries do not support unloading
    // See https://github.com/dotnet/corert/issues/7887
    return result;
}

char *callMergeStrArray( char **array, int srcCount)
{
    const char* funcName = "mergestrings";
    const int tgtSize = 128;
    char *oput = malloc(tgtSize * sizeof(char));
    // Declare a typedef
    typedef void (*myFunc)();

    // Import Symbol named funcName
    myFunc MyImport = (myFunc)symLoad(*handle, funcName);

    MyImport(oput, array, srcCount, tgtSize);
    
    return oput;
}

void *callPopulateStruct(void *structure)
{
    const char *funcName = "popstruct";
    // Declare a typedef
    typedef void (*myFunc)();

    // Import Symbol named funcName
    myFunc MyImport = (myFunc)symLoad(*handle, funcName);

    MyImport(structure);
}
