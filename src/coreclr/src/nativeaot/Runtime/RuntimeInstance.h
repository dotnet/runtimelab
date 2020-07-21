// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#ifndef __RuntimeInstance_h__
#define __RuntimeInstance_h__

class ThreadStore;
typedef DPTR(ThreadStore) PTR_ThreadStore;
class ICodeManager;
struct StaticGcDesc;
typedef SPTR(StaticGcDesc) PTR_StaticGcDesc;
class TypeManager;
enum GenericVarianceType : UInt8;

#include "ICodeManager.h"

class RuntimeInstance
{
    friend class AsmOffsets;
    friend struct DefaultSListTraits<RuntimeInstance>;
    friend class Thread;

    PTR_ThreadStore             m_pThreadStore;
    HANDLE                      m_hPalInstance; // this is the HANDLE passed into DllMain
    ReaderWriterLock            m_ModuleListLock;

public:
    struct OsModuleEntry;
    typedef DPTR(OsModuleEntry) PTR_OsModuleEntry;
    struct OsModuleEntry
    {
        PTR_OsModuleEntry      m_pNext;
        HANDLE                 m_osModule;
    };

    typedef SList<OsModuleEntry> OsModuleList;
private:
    OsModuleList                m_OsModuleList;

    struct CodeManagerEntry;
    typedef DPTR(CodeManagerEntry) PTR_CodeManagerEntry;

    struct CodeManagerEntry
    {
        PTR_CodeManagerEntry    m_pNext;
        PTR_VOID                m_pvStartRange;
        UInt32                  m_cbRange;
        ICodeManager *          m_pCodeManager;
    };

    typedef SList<CodeManagerEntry> CodeManagerList;
    CodeManagerList             m_CodeManagerList;

public:
    struct TypeManagerEntry
    {
        TypeManagerEntry*         m_pNext;
        TypeManager*              m_pTypeManager;
    };

    typedef SList<TypeManagerEntry> TypeManagerList;
    
private:
    TypeManagerList             m_TypeManagerList;

    bool                        m_conservativeStackReportingEnabled;

    struct  UnboxingStubsRegion
    {
        PTR_VOID                m_pRegionStart;
        UInt32                  m_cbRegion;
        UnboxingStubsRegion*    m_pNextRegion;

        UnboxingStubsRegion() : m_pRegionStart(0), m_cbRegion(0), m_pNextRegion(NULL) { }
    };

    UnboxingStubsRegion*        m_pUnboxingStubsRegion;

    RuntimeInstance();

    SList<Module>* GetModuleList();

    SList<TypeManager*>* GetModuleManagerList();

    bool BuildGenericTypeHashTable();

    ICodeManager * FindCodeManagerForClasslibFunction(PTR_VOID address);

public:
    ~RuntimeInstance();
    ThreadStore *   GetThreadStore();
    HANDLE          GetPalInstance();

    PTR_UInt8 FindMethodStartAddress(PTR_VOID ControlPC);
    PTR_UInt8 GetTargetOfUnboxingAndInstantiatingStub(PTR_VOID ControlPC);
    void EnableConservativeStackReporting();
    bool IsConservativeStackReportingEnabled() { return m_conservativeStackReportingEnabled; }

    bool RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);
    void UnregisterCodeManager(ICodeManager * pCodeManager);

    ICodeManager * FindCodeManagerByAddress(PTR_VOID ControlPC);
    PTR_VOID GetClasslibFunctionFromCodeAddress(PTR_VOID address, ClasslibFunctionId functionId);

    bool RegisterTypeManager(TypeManager * pTypeManager);
    TypeManagerList& GetTypeManagerList();
    OsModuleList* GetOsModuleList();
    ReaderWriterLock& GetTypeManagerLock();

    bool RegisterUnboxingStubs(PTR_VOID pvStartRange, UInt32 cbRange);
    bool IsUnboxingStub(UInt8* pCode);

    static bool Initialize(HANDLE hPalInstance);
    void Destroy();

    void EnumAllStaticGCRefs(void * pfnCallback, void * pvCallbackData);

    bool ShouldHijackCallsiteForGcStress(UIntNative CallsiteIP);
    bool ShouldHijackLoopForGcStress(UIntNative CallsiteIP);
    void SetLoopHijackFlags(UInt32 flag);
};
typedef DPTR(RuntimeInstance) PTR_RuntimeInstance;


PTR_RuntimeInstance GetRuntimeInstance();

#endif // __RuntimeInstance_h__
