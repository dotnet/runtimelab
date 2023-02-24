project (HelloWasmNative)
include_directories(${INC_PLATFORM_DIR})

add_library (HelloWasmNative HelloWasmNative.cpp)
target_link_libraries(HelloWasmNative ${LINK_LIBRARIES_ADDITIONAL})

# add the install targets
install (TARGETS HelloWasmNative DESTINATION ${TEST_LIBRARY_DESTINATION})
