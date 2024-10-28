.assembly extern System.Console { .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A ) }
.assembly extern System.Runtime { .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A ) }

.assembly App { }

.class public auto ansi beforefieldinit Program
    extends [System.Runtime]System.Object
{
    .method private hidebysig static
        void Main (
            string[]
        ) cil managed
    {
        .entrypoint

        call void Program::Call()
        ret
    }

    .method public hidebysig specialname rtspecialname
        instance void .ctor () cil managed
    {
        ldarg.0
        call instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method private hidebysig static
        void Call () cil managed
    {
        .locals init (
            [0] method instance void *(object),
            [1] method void *(object)
        )

        // Instance
        ldftn instance void Program::MI(object)
        stloc.0
        newobj instance void Program::.ctor()
        ldnull
        ldloc.0
        calli instance void(object)

        // Static
        ldftn void Program::MS(object)
        stloc.1
        ldnull
        ldloc.1
        calli void(object)

        ret
    }

    .method private hidebysig static
        void MS (
            object o
        ) cil managed
    {
        ldstr "Hello, World! - static"
        call void [System.Console]System.Console::WriteLine(string)
        ret
    }

    .method private hidebysig
        void MI (
            object o
        ) cil managed
    {
        ldstr "Hello, World! - instance"
        call void [System.Console]System.Console::WriteLine(string)
        ret
    }
}