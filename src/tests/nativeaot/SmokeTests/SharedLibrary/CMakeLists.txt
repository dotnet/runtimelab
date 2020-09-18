project (SharedLibrary)
include_directories(${INC_PLATFORM_DIR})

if (CLR_CMAKE_TARGET_UNIX)
    target_link_libraries (SharedLibrary ${CMAKE_DL_LIBS})
endif()

add_executable (SharedLibrary SharedLibrary.cpp)

# add the install targets
install (TARGETS PInvokeNative DESTINATION bin)
