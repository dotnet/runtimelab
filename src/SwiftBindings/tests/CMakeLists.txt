cmake_minimum_required(VERSION 3.24)

project(SmokeTests Swift)

set(SOURCE SmokeTests)

if (NOT SWIFT_COMPILER_TARGET)
    set(SWIFT_PLATFORM "macosx")
    if (NOT DEFINED CMAKE_OSX_ARCHITECTURES OR CMAKE_OSX_ARCHITECTURES STREQUAL "")
        set(CMAKE_OSX_ARCHITECTURES "arm64")
    endif()
    set(SWIFT_PLATFORM_SUFFIX "11")
    set(SWIFT_DEPLOYMENT_TARGET ${CMAKE_OSX_DEPLOYMENT_TARGET})
    set(SWIFT_COMPILER_TARGET "${CMAKE_OSX_ARCHITECTURES}-apple-${SWIFT_PLATFORM}${SWIFT_DEPLOYMENT_TARGET}${SWIFT_PLATFORM_SUFFIX}")
endif()

add_custom_target(${SOURCE} ALL
    COMMAND xcrun swiftc -target ${SWIFT_COMPILER_TARGET} -emit-module -emit-library -enable-library-evolution -emit-module-interface ${CMAKE_CURRENT_SOURCE_DIR}/${SOURCE}.swift  -o ${CMAKE_CURRENT_BINARY_DIR}/lib${SOURCE}.dylib
    DEPENDS ${CMAKE_CURRENT_SOURCE_DIR}/${SOURCE}.swift
    COMMENT "Generating ${SOURCE} library"
)

install(FILES ${CMAKE_CURRENT_BINARY_DIR}/lib${SOURCE}.dylib
    DESTINATION bin
)
