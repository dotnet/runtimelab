project (SharedLibrary)
include_directories(${INC_PLATFORM_DIR})

add_executable (SharedLibrary SharedLibrary.cpp)

# add the install targets
install (TARGETS PInvokeNative DESTINATION bin)
