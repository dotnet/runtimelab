set(CMAKE_INCLUDE_CURRENT_DIR ON)

include_directories(../gc_lxr ../gc_lxr/tests)

if(CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  add_compile_definitions(FEATURE_LXR_RC_VERTICAL)
endif()

if(FEATURE_STANDALONE_GC)
  add_definitions(-DFEATURE_STANDALONE_GC)
  remove_definitions(-DSTRESS_HEAP)
  remove_definitions(-DWRITE_BARRIER_CHECK)
endif(FEATURE_STANDALONE_GC)

set(GC_SOURCES
  gceventstatus.cpp
  gcconfig.cpp
  gccommon.cpp
  gcscan.cpp
  handletable.cpp
  handletablecore.cpp
  handletablescan.cpp
  objecthandle.cpp
  ../gc_lxr/side_metadata.cpp
  softwarewritewatch.cpp
  gchandletable.cpp
  gcload.cpp
  gcbridge.cpp
  handletablecache.cpp)

if(CLR_CMAKE_TARGET_ARCH_WASM)
  add_subdirectory(wasm)
elseif(CLR_CMAKE_HOST_UNIX)
  add_subdirectory(unix)
  include(unix/configure.cmake)
else()
  add_subdirectory(windows)
  set (GC_SOURCES
    ${GC_SOURCES}
    windows/Native.rc)
endif()

if (CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)
  add_subdirectory(vxsort)
endif (CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)

if (CLR_CMAKE_TARGET_WIN32)
  set(GC_HEADERS
    env/common.h
    env/etmdummy.h
    env/gcenv.base.h
    env/gcenv.ee.h
    env/gcenv.h
    env/gcenv.interlocked.h
    env/gcenv.interlocked.inl
    env/gcenv.object.h
    env/gcenv.os.h
    env/gcenv.structs.h
    env/gcenv.sync.h
    env/gcenv.windows.inl
    env/volatile.h
    gc.h
    gcinternal.h
    gcconfig.h
    gcbridge.h
    gcdesc.h
    gcenv.ee.standalone.inl
    gcenv.inl
    gcevent_serializers.h
    gcevents.h
    gceventstatus.h
    gchandletableimpl.h
    gcimpl.h
    gcinterface.dac.h
    gcinterface.ee.h
    gcinterface.h
    gcpriv.h
    gcrecord.h
    gcscan.h
    handletable.h
    handletable.inl
    handletablepriv.h
    objecthandle.h
    ../gc_lxr/side_metadata.h
    softwarewritewatch.h
  )
endif(CLR_CMAKE_TARGET_WIN32)

if(CLR_CMAKE_HOST_WIN32)
  set (GC_LINK_LIBRARIES
    ${STATIC_MT_CRT_LIB}
    ${STATIC_MT_VCRT_LIB}
    kernel32.lib
    advapi32.lib
    coreclrminipal)
endif(CLR_CMAKE_HOST_WIN32)

set (GC_LINK_LIBRARIES
    ${GC_LINK_LIBRARIES}
    gc_pal
    minipal)

if(CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)
    list(APPEND GC_LINK_LIBRARIES
        gc_vxsort
    )
endif(CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)


list(APPEND GC_SOURCES ${GC_HEADERS})

convert_to_absolute_path(GC_SOURCES ${GC_SOURCES})

set(GC_WKS_SVR_SOURCES
  gcee.cpp
  gc.cpp
  init.cpp
  no_gc.cpp
  finalization.cpp
  dynamic_tuning.cpp
  region_free_list.cpp
  region_allocator.cpp
  memory.cpp
  sweep.cpp
  collect.cpp
  diagnostics.cpp
  dynamic_heap_count.cpp
  card_table.cpp
  relocate_compact.cpp
  mark_phase.cpp
  background.cpp
  interface.cpp
  allocation.cpp
  plan_phase.cpp
  regions_segments.cpp)

if(CLR_CMAKE_ENABLE_OBJECT_REFERENCE_ENUMERATION_TEST)
  list(APPEND GC_SOURCES objectreferenceenumerationtest.cpp)
endif()

if(CLR_CMAKE_ENABLE_IMMIX_BLOCK_STATE_TEST OR CLR_CMAKE_ENABLE_REFERENCE_COUNT_TEST OR
   CLR_CMAKE_ENABLE_IMMIX_ALLOCATION_TEST OR CLR_CMAKE_ENABLE_LARGE_OBJECT_SPACE_TEST OR
   CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES ../gc_lxr/immix_block.cpp)
endif()

if(CLR_CMAKE_ENABLE_IMMIX_BLOCK_STATE_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/immixblockstatetest.cpp)
endif()

if(CLR_CMAKE_ENABLE_REFERENCE_COUNT_TEST OR CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES
    ../gc_lxr/reference_count.cpp
    ../gc_lxr/reference_count.h)
endif()

if(CLR_CMAKE_ENABLE_REFERENCE_COUNT_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/referencecounttest.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_WORK_PACKET_SCHEDULER_TEST OR
   CLR_CMAKE_ENABLE_LXR_DEFERRED_BUCKET_TEST OR
   CLR_CMAKE_ENABLE_LXR_LAZY_COMPLETION_COUNTER_TEST OR
   CLR_CMAKE_ENABLE_LXR_PAUSE_COORDINATOR_TEST OR CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES
    ../gc_lxr/lxr_work_packet.cpp
    ../gc_lxr/lxr_work_scheduler.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_LAZY_COMPLETION_COUNTER_TEST OR CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES ../gc_lxr/lxr_completion_counter.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_LAZY_COMPLETION_COUNTER_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/lxrcompletioncountertest.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_WORK_PACKET_SCHEDULER_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/lxrworkpacketschedulertest.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_DEFERRED_BUCKET_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/lxrdeferredbuckettest.cpp)
endif()

if(CLR_CMAKE_ENABLE_IMMIX_ALLOCATION_TEST OR CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES ../gc_lxr/immix_allocation.cpp)
endif()

if(CLR_CMAKE_ENABLE_IMMIX_ALLOCATION_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/immixallocationtest.cpp)
endif()

if(CLR_CMAKE_ENABLE_LARGE_OBJECT_SPACE_TEST OR CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES ../gc_lxr/large_object_space.cpp)
endif()

if(CLR_CMAKE_ENABLE_LARGE_OBJECT_SPACE_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/largeobjectspacetest.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_PAUSE_COORDINATOR_TEST OR CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES
    ../gc_lxr/lxr_pause_coordinator.cpp
    ../gc_lxr/lxr_pause_schedule.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_PAUSE_COORDINATOR_TEST)
  list(APPEND GC_SOURCES ../gc_lxr/tests/lxrpausecoordinatortest.cpp)
endif()

if(CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  list(APPEND GC_SOURCES
    ../gc_lxr/lxr_reference_counting.cpp ../gc_lxr/lxr_reference_counting.h
    ../gc_lxr/tests/lxrrcverticaltest.cpp ../gc_lxr/tests/lxrrcverticaltest.h)
endif()

if(FEATURE_STANDALONE_GC)
  if (CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)
    set(BUILD_EXP_GC 1)
  endif()

  # clrgcexp is build with standalone+regions
  if (BUILD_EXP_GC)
    add_library_clr(clrgcexp_gc_wks OBJECT ${GC_WKS_SVR_SOURCES})
    set_target_properties(clrgcexp_gc_wks PROPERTIES UNITY_BUILD ON UNITY_BUILD_BATCH_SIZE 0)
    add_dependencies(clrgcexp_gc_wks eventing_headers)
    target_compile_definitions(clrgcexp_gc_wks PRIVATE USE_REGIONS)
    set(CLRGGCEXP_OBJECTS
      $<TARGET_OBJECTS:clrgcexp_gc_wks>)
    if (FEATURE_SVR_GC)
      add_library_clr(clrgcexp_gc_svr OBJECT ${GC_WKS_SVR_SOURCES})
      set_target_properties(clrgcexp_gc_svr PROPERTIES UNITY_BUILD ON UNITY_BUILD_BATCH_SIZE 0)
      add_dependencies(clrgcexp_gc_svr eventing_headers)
      target_compile_definitions(clrgcexp_gc_svr PRIVATE SERVER_GC USE_REGIONS)
      list(APPEND CLRGGCEXP_OBJECTS
        $<TARGET_OBJECTS:clrgcexp_gc_svr>)
    endif()

    add_library_clr(clrgcexp SHARED ${GC_SOURCES} ${CLRGGCEXP_OBJECTS})
    add_dependencies(clrgcexp eventing_headers)
    target_link_libraries(clrgcexp PRIVATE ${GC_LINK_LIBRARIES})
    target_link_libraries(clrgcexp PRIVATE gcexp_dll_wks_descriptor)
    if (FEATURE_SVR_GC)
      target_link_libraries(clrgcexp PRIVATE gcexp_dll_svr_descriptor)
    endif()
    target_compile_definitions(clrgcexp PRIVATE -DUSE_REGIONS)
    install_clr(TARGETS clrgcexp DESTINATIONS . COMPONENT runtime)
  endif (BUILD_EXP_GC)

  add_library_clr(clrgc_gc_wks OBJECT ${GC_WKS_SVR_SOURCES})
  add_dependencies(clrgc_gc_wks eventing_headers)
  if(CLR_CMAKE_ENABLE_OBJECT_REFERENCE_ENUMERATION_TEST)
    target_compile_definitions(clrgc_gc_wks PRIVATE FEATURE_OBJECT_REFERENCE_ENUMERATION_TEST)
  endif()
  if(CLR_CMAKE_ENABLE_OBJECT_HEADER_BITS_TEST)
    target_compile_definitions(clrgc_gc_wks PRIVATE FEATURE_OBJECT_HEADER_BITS_TEST)
  endif()
  set(CLRGC_OBJECTS
    $<TARGET_OBJECTS:clrgc_gc_wks>)
  if(FEATURE_SVR_GC)
    add_library_clr(clrgc_gc_svr OBJECT ${GC_WKS_SVR_SOURCES})
    add_dependencies(clrgc_gc_svr eventing_headers)
    target_compile_definitions(clrgc_gc_svr PRIVATE SERVER_GC)
    if(CLR_CMAKE_ENABLE_OBJECT_REFERENCE_ENUMERATION_TEST)
      target_compile_definitions(clrgc_gc_svr PRIVATE FEATURE_OBJECT_REFERENCE_ENUMERATION_TEST)
    endif()
    if(CLR_CMAKE_ENABLE_OBJECT_HEADER_BITS_TEST)
      target_compile_definitions(clrgc_gc_svr PRIVATE FEATURE_OBJECT_HEADER_BITS_TEST)
    endif()
    list(APPEND CLRGC_OBJECTS
      $<TARGET_OBJECTS:clrgc_gc_svr>)
  endif()

  # clrgc is build with standalone+segments
  add_library_clr(clrgc SHARED ${GC_SOURCES} ${CLRGC_OBJECTS})
  add_dependencies(clrgc eventing_headers)
  if(CLR_CMAKE_ENABLE_OBJECT_REFERENCE_ENUMERATION_TEST)
    target_compile_definitions(clrgc PRIVATE FEATURE_OBJECT_REFERENCE_ENUMERATION_TEST)
  endif()
  if(CLR_CMAKE_ENABLE_OBJECT_HEADER_BITS_TEST)
    target_compile_definitions(clrgc PRIVATE FEATURE_OBJECT_HEADER_BITS_TEST)
  endif()
  target_link_libraries(clrgc PRIVATE ${GC_LINK_LIBRARIES})
  target_link_libraries(clrgc PRIVATE gc_dll_wks_descriptor)
  if(FEATURE_SVR_GC)
    target_link_libraries(clrgc PRIVATE gc_dll_svr_descriptor)
  endif()
  install_clr(TARGETS clrgc DESTINATIONS . COMPONENT runtime)

  add_definitions(-DBUILD_AS_STANDALONE)
  add_definitions(-DFEATURE_CONSERVATIVE_GC)

  add_definitions(-DFX_VER_INTERNALNAME_STR=clrgc.dll)
  add_definitions(-DVERIFY_HEAP)
  add_definitions(-DGC_CONFIG_DRIVEN)
  if(CLR_CMAKE_HOST_APPLE)
    # The implementation of GCToOSInterface on Apple platforms makes use of non-POSIX
    # pthreads APIs, which by default are not included in the pthreads header
    # unless we define this macro.
    add_definitions(-D_DARWIN_C_SOURCE)
  endif(CLR_CMAKE_HOST_APPLE)

  include_directories(${CMAKE_CURRENT_SOURCE_DIR})
  include_directories(${CMAKE_CURRENT_SOURCE_DIR}/env)

  add_definitions(-DGC_DESCRIPTOR)
  add_subdirectory(datadescriptor)

  install_clr(TARGETS clrgc DESTINATIONS . sharedFramework COMPONENT runtime)
  if (CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)
    install_clr(TARGETS clrgcexp DESTINATIONS . sharedFramework COMPONENT runtime)
  endif (CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)
endif(FEATURE_STANDALONE_GC)
