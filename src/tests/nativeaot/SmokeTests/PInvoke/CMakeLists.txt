project (PInvokeNative)
include_directories(${INC_PLATFORM_DIR})

if(CLR_CMAKE_HOST_WIN32)
    list(APPEND LINK_LIBRARIES_ADDITIONAL
        ole32.lib
    )
endif(CLR_CMAKE_HOST_WIN32)

<<<<<<< HEAD
add_library (PInvokeNative PInvokeNative.cpp)
target_link_libraries(PInvokeNative ${LINK_LIBRARIES_ADDITIONAL})
=======
add_library (PInvokeNative SHARED PInvokeNative.cpp)
target_link_libraries(PInvokeNative PRIVATE ${LINK_LIBRARIES_ADDITIONAL})
>>>>>>> runtime/main

# add the install targets
install (TARGETS PInvokeNative DESTINATION ${TEST_LIBRARY_DESTINATION})
