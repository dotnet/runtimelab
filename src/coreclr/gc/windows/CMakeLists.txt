set(CMAKE_INCLUDE_CURRENT_DIR ON)
include_directories("../env")
include_directories("..")

set(GC_PAL_SOURCES
    gcenv.windows.cpp
    gate.cpp
)

add_library(gc_pal STATIC ${GC_PAL_SOURCES})
target_link_libraries(gc_pal PUBLIC Synchronization.lib)
