extern "C" int ReturnsPrimitiveInt();
extern "C" bool ReturnsPrimitiveBool();
extern "C" unsigned short ReturnsPrimitiveChar();
extern "C" void EnsureManagedClassLoaders();
extern "C" int CheckSimpleGCCollect();
extern "C" int CheckSimpleExceptionHandling();

int main()
{
    if (ReturnsPrimitiveInt() != 10)
        return 1;

    if (!ReturnsPrimitiveBool())
        return 2;

    if (ReturnsPrimitiveChar() != 'a')
        return 3;

    // As long as no unmanaged exception is thrown
    // managed class loaders were initialized successfully
    EnsureManagedClassLoaders();

    if (CheckSimpleGCCollect() != 100)
        return 4;

    if (CheckSimpleExceptionHandling() != 100)
        return 5;

    return 0;
}
