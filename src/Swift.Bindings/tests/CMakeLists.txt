cmake_minimum_required(VERSION 3.24)

project(SwiftTests)

# Find all Swift source files in the directory
file(GLOB SOURCES "*.swift")

if (NOT SWIFT_COMPILER_TARGET)
    set(SWIFT_PLATFORM "macosx")
    if (NOT DEFINED CMAKE_OSX_ARCHITECTURES OR CMAKE_OSX_ARCHITECTURES STREQUAL "")
        set(CMAKE_OSX_ARCHITECTURES "${CMAKE_SYSTEM_PROCESSOR}")
    endif()
    set(SWIFT_DEPLOYMENT_TARGET ${CMAKE_OSX_DEPLOYMENT_TARGET})
    if (NOT DEFINED SWIFT_DEPLOYMENT_TARGET OR SWIFT_DEPLOYMENT_TARGET STREQUAL "")
        set(SWIFT_DEPLOYMENT_TARGET "11")
    endif()
    set(SWIFT_COMPILER_TARGET "${CMAKE_OSX_ARCHITECTURES}-apple-${SWIFT_PLATFORM}${SWIFT_DEPLOYMENT_TARGET}")
endif()

foreach(SOURCE_FILE ${SOURCES})
    get_filename_component(SOURCE_BASE_NAME ${SOURCE_FILE} NAME_WE)
    add_custom_target(${SOURCE_BASE_NAME} ALL
        COMMAND xcrun swiftc -target ${SWIFT_COMPILER_TARGET} -emit-module -emit-library -enable-library-evolution -emit-module-interface ${SOURCE_FILE} -o ${CMAKE_CURRENT_BINARY_DIR}/lib${SOURCE_BASE_NAME}.dylib
        DEPENDS ${SOURCE_FILE}
        COMMENT "Generating ${SOURCE_BASE_NAME} library"
    )
    install(FILES ${CMAKE_CURRENT_BINARY_DIR}/lib${SOURCE_BASE_NAME}.dylib
        DESTINATION bin
    )
endforeach()
