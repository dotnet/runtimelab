set(CMAKE_INCLUDE_CURRENT_DIR ON)

add_definitions(-DFEATURE_STANDALONE_GC)
remove_definitions(-DSTRESS_HEAP)
remove_definitions(-DWRITE_BARRIER_CHECK)

add_definitions(-DBUILD_AS_STANDALONE)
add_definitions(-DFEATURE_CONSERVATIVE_GC)
add_definitions(-DFX_VER_INTERNALNAME_STR=clrlxrgc.dll)
add_definitions(-DVERIFY_HEAP)
add_definitions(-DGC_CONFIG_DRIVEN)
add_definitions(-DGC_DESCRIPTOR)
if(CLR_CMAKE_ENABLE_LXR_RC_VERTICAL)
  add_compile_definitions(FEATURE_LXR_RC_VERTICAL)
endif()
if(CLR_CMAKE_ENABLE_LXR_PHASE_TIMERS)
  add_compile_definitions(LXR_ENABLE_PHASE_TIMERS)
endif()

include_directories(../gc ../gc/env ../pal/inc)
include_directories(${CMAKE_CURRENT_BINARY_DIR}/../gc)

set(LXR_SOURCES
  ../gc/gceventstatus.cpp
  ../gc/gcconfig.cpp
  ../gc/gccommon.cpp
  ../gc/gcscan.cpp
  ../gc/handletable.cpp
  ../gc/handletablecore.cpp
  ../gc/handletablescan.cpp
  ../gc/handletablecache.cpp
  ../gc/objecthandle.cpp
  ../gc/gchandletable.cpp
  ../gc/gcbridge.cpp
  ../gc/softwarewritewatch.cpp
  side_metadata.cpp
  immix_block.cpp
  reference_count.cpp
  immix_allocation.cpp
  immix_zeroing.cpp
  large_object_space.cpp
  lxr_work_packet.cpp
  lxr_work_scheduler.cpp
  lxr_completion_counter.cpp
  lxr_pause_schedule.cpp
  lxr_pause_coordinator.cpp
  lxr_reference_counting.cpp
  lxr_finalizer_registry.h
  lxr_finalizer_pause.h
  lxr_pin_list.h
  lxr_young_spans.h
  lxr_gc.cpp
  lxr_gc.h
  lxr_gcload.cpp
  lxr_write_barrier.cpp
  lxr_write_barrier.h
  lxr_block_zeroer.h
  lxr_worker_policy.h
  lxr_prefetch.h)

if(CLR_CMAKE_TARGET_WIN32)
  list(APPEND LXR_SOURCES ../gc/windows/Native.rc)
  set(LXR_WRITE_BARRIER_ASM amd64/lxr_writebarrier.asm)
else()
  set(LXR_WRITE_BARRIER_ASM amd64/lxr_writebarrier.S)
endif()
add_library_clr(lxr_writebarrier OBJECT ${LXR_WRITE_BARRIER_ASM})
target_include_directories(lxr_writebarrier PRIVATE
  ${ARCH_SOURCES_DIR}
  ../vm/${ARCH_SOURCES_DIR}
  ${CMAKE_CURRENT_BINARY_DIR}/../vm/wks)
if(CLR_CMAKE_TARGET_WIN32)
  add_dependencies(lxr_writebarrier asmconstants_inc)
endif()

if(CLR_CMAKE_HOST_WIN32)
  set(LXR_LINK_LIBRARIES
    ${STATIC_MT_CRT_LIB}
    ${STATIC_MT_VCRT_LIB}
    kernel32.lib
    advapi32.lib
    coreclrminipal)
endif()
list(APPEND LXR_LINK_LIBRARIES gc_pal minipal)
if(CLR_CMAKE_TARGET_ARCH_ARM64 OR CLR_CMAKE_TARGET_ARCH_AMD64)
  list(APPEND LXR_LINK_LIBRARIES gc_vxsort)
endif()

add_library_clr(clrlxrgc SHARED ${LXR_SOURCES} $<TARGET_OBJECTS:lxr_writebarrier>)
add_dependencies(clrlxrgc eventing_headers)
target_link_libraries(clrlxrgc PRIVATE ${LXR_LINK_LIBRARIES})
# The default library finds object starts from the count plane (no ValidObject plane).
if(NOT CLR_CMAKE_LXR_OBJECT_START_BITMAP)
  target_compile_definitions(clrlxrgc PRIVATE LXR_OBJECT_START_WALK)
endif()
install_clr(TARGETS clrlxrgc DESTINATIONS . sharedFramework COMPONENT runtime)

# Second library built from the same sources for comparison: object starts come from the
# ValidObject bit plane. Select it with DOTNET_GCPath=<dir>/libclrlxrgc-bitmap.so.
if(CLR_CMAKE_LXR_BUILD_BITMAP_VARIANT AND NOT CLR_CMAKE_LXR_OBJECT_START_BITMAP)
  add_library_clr(clrlxrgc_bitmap SHARED ${LXR_SOURCES} $<TARGET_OBJECTS:lxr_writebarrier>)
  add_dependencies(clrlxrgc_bitmap eventing_headers)
  target_link_libraries(clrlxrgc_bitmap PRIVATE ${LXR_LINK_LIBRARIES})
  set_target_properties(clrlxrgc_bitmap PROPERTIES OUTPUT_NAME clrlxrgc-bitmap)
  install_clr(TARGETS clrlxrgc_bitmap DESTINATIONS . sharedFramework COMPONENT runtime)
endif()
