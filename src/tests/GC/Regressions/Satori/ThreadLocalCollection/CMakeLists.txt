project(ThreadLocalCollectionOutcomesNative)

if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    add_library(ThreadLocalCollectionOutcomesNative SHARED LocalCollectionOutcomes.cpp)
    target_include_directories(ThreadLocalCollectionOutcomesNative PRIVATE
        ${INC_PLATFORM_DIR}
        ${CLR_REPO_ROOT_DIR}/src/coreclr/gc_satori)
    target_compile_definitions(ThreadLocalCollectionOutcomesNative PRIVATE THREAD_LOCAL_COLLECTION_TEST_LIBRARY)
    target_link_libraries(ThreadLocalCollectionOutcomesNative PRIVATE ${LINK_LIBRARIES_ADDITIONAL})
    install(TARGETS ThreadLocalCollectionOutcomesNative DESTINATION bin)
endif()
