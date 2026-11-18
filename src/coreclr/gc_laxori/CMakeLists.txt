set(CMAKE_INCLUDE_CURRENT_DIR ON)

add_definitions(-DFEATURE_STANDALONE_GC)
remove_definitions(-DSTRESS_HEAP)
remove_definitions(-DWRITE_BARRIER_CHECK)

add_definitions(-DBUILD_AS_STANDALONE)
add_definitions(-DFEATURE_CONSERVATIVE_GC)
add_definitions(-DFX_VER_INTERNALNAME_STR=clrlaxorigc.dll)
add_definitions(-DVERIFY_HEAP)
add_definitions(-DGC_CONFIG_DRIVEN)
add_definitions(-DGC_DESCRIPTOR)
add_compile_definitions(LAXORI_ENABLE_LAZY_COMPLETION_COUNTER)
if(CLR_CMAKE_ENABLE_LAXORI_PHASE_TIMERS)
  add_compile_definitions(LAXORI_ENABLE_PHASE_TIMERS)
endif()

include_directories(../gc ../gc/env ../pal/inc)
include_directories(${CMAKE_CURRENT_BINARY_DIR}/../gc)

set(LAXORI_SOURCES
  ../gc/gceventstatus.cpp
  ../gc/gcconfig.cpp
  ../gc/gccommon.cpp
  ../gc/gcscan.cpp
  ../gc/handletable.cpp
  ../gc/handletablecore.cpp
  ../gc/handletablescan.cpp
  ../gc/handletablecache.cpp
  ../gc/objecthandle.cpp
  laxori_gchandletable.cpp
  ../gc/gcbridge.cpp
  ../gc/softwarewritewatch.cpp
  laxori_side_metadata.cpp
  laxori_immix_block.cpp
  laxori_reference_count.cpp
  laxori_immix_allocation.cpp
  laxori_immix_zeroing.cpp
  laxori_large_object_space.cpp
  laxori_work_packet.cpp
  laxori_work_scheduler.cpp
  laxori_completion_counter.cpp
  laxori_pause_schedule.cpp
  laxori_pause_coordinator.cpp
  laxori_reference_counting.cpp
  laxori_finalizer_registry.h
  laxori_finalizer_pause.h
  laxori_pin_list.h
  laxori_young_spans.h
  laxori_gc.cpp
  laxori_private.cpp
  laxori_private.h
  laxori_gc.h
  laxori_gcload.cpp
  laxori_write_barrier.cpp
  laxori_write_barrier.h
  laxori_block_zeroer.h
  laxori_worker_policy.h
  laxori_prefetch.h)

if(CLR_CMAKE_TARGET_WIN32)
  list(APPEND LAXORI_SOURCES ../gc/windows/Native.rc)
  set(LAXORI_WRITE_BARRIER_ASM amd64/laxori_writebarrier.asm)
else()
  set(LAXORI_WRITE_BARRIER_ASM amd64/laxori_writebarrier.S)
endif()
add_library_clr(laxori_writebarrier OBJECT ${LAXORI_WRITE_BARRIER_ASM})
target_include_directories(laxori_writebarrier PRIVATE
  ${ARCH_SOURCES_DIR}
  ../vm/${ARCH_SOURCES_DIR}
  ${CMAKE_CURRENT_BINARY_DIR}/../vm/wks)
if(CLR_CMAKE_TARGET_WIN32)
  add_dependencies(laxori_writebarrier asmconstants_inc)
endif()

if(CLR_CMAKE_HOST_WIN32)
  set(LAXORI_LINK_LIBRARIES
    ${STATIC_MT_CRT_LIB}
    ${STATIC_MT_VCRT_LIB}
    kernel32.lib
    advapi32.lib
    coreclrminipal)
endif()
list(APPEND LAXORI_LINK_LIBRARIES gc_pal minipal)
list(APPEND LAXORI_LINK_LIBRARIES gc_vxsort)

add_library_clr(clrlaxorigc SHARED ${LAXORI_SOURCES} $<TARGET_OBJECTS:laxori_writebarrier>)
add_dependencies(clrlaxorigc eventing_headers)
target_link_libraries(clrlaxorigc PRIVATE ${LAXORI_LINK_LIBRARIES})
install_clr(TARGETS clrlaxorigc DESTINATIONS . sharedFramework COMPONENT runtime)

# The default library finds object starts from the count plane (no ValidObject plane), like the default LXR library.
if(NOT CLR_CMAKE_LAXORI_OBJECT_START_BITMAP)
  target_compile_definitions(clrlaxorigc PRIVATE LAXORI_OBJECT_START_WALK)
endif()

# Second library built from the same sources for comparison: object starts come from the ValidObject bit plane.
# Select it with DOTNET_GCPath=<dir>/libclrlaxorigc-bitmap.so.
if(CLR_CMAKE_LAXORI_BUILD_BITMAP_VARIANT AND NOT CLR_CMAKE_LAXORI_OBJECT_START_BITMAP)
  add_library_clr(clrlaxorigc_bitmap SHARED ${LAXORI_SOURCES} $<TARGET_OBJECTS:laxori_writebarrier>)
  add_dependencies(clrlaxorigc_bitmap eventing_headers)
  target_link_libraries(clrlaxorigc_bitmap PRIVATE ${LAXORI_LINK_LIBRARIES})
  set_target_properties(clrlaxorigc_bitmap PROPERTIES OUTPUT_NAME clrlaxorigc-bitmap)
  install_clr(TARGETS clrlaxorigc_bitmap DESTINATIONS . sharedFramework COMPONENT runtime)
endif()
