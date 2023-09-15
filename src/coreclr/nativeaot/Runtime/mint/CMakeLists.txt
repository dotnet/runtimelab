project(mint C)

set(NATIVEAOT_MINT 1)
set(NATIVEAOT_MINT_INC_PATH ${CMAKE_CURRENT_SOURCE_DIR}/inc)

set(SHARED_CONTAINERS_SOURCE_PATH "${CLR_SRC_NATIVE_DIR}/containers")
set(SHARED_MINIPAL_SOURCE_PATH "${CLR_SRC_NATIVE_DIR}/minipal")
set(MONO_BASEDIR_PATH "${CLR_SRC_NATIVE_DIR}/../mono/mono")
set(MONO_INTERP_SOURCES_PATH "${MONO_BASEDIR_PATH}/mini/interp")

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_EXTENSIONS ON)

set(interp_files
    mintops.h
    interp-internals.h
    interp.h
    transform.h
    tiering.h
    mintops.c
    transform.c
    interp-imethod.c
    interp.c
)

set(mono_metadata_files
  mint-abstraction.h
  opcodes.c
  mono-basic-block.c
)

set(mono_utils_files
  monobitset.c
)

addprefix(interp_sources "${MONO_INTERP_SOURCES_PATH}" "${interp_files}")

addprefix(mono_metadata_sources "${MONO_BASEDIR_PATH}/metadata" "${mono_metadata_files}")
addprefix(mono_utils_sources "${MONO_BASEDIR_PATH}/utils" "${mono_utils_files}")

# "${MONO_INTERP_SOURCES_PATH}/transform.c"
set(MINT_SOURCES
    ${mono_utils_sources}
    ${mono_metadata_sources}
    ${interp_sources}
    inc/mint-abstraction-nativeaot.h
    inc/monoshim/missing-symbols.h
    inc/monoshim/utils/mono-publib.h
    inc/monoshim/utils/mono-compiler.h
    inc/prebuilt/osx-arm64/config.h
    inc/prebuilt/osx-arm64/eglib-config.h
    inc/mint-transform.h
    inc/mint-imethod.h
    mint.c
    mint-itf.c
    mint-ee-itf.c
    mint-ee.c)

set(NATIVEAOT_MINT_PREBUILT_OS "${CLR_CMAKE_TARGET_OS}")

if(NATIVEAOT_MINT_PREBUILT_OS STREQUAL "darwin")
    if(CMAKE_SYSTEM_VARIANT STREQUAL maccatalyst)
        set(NATIVEAOT_MINT_PREBUILT_OS "maccatalyst")
    else()
        set(NATIVEAOT_MINT_PREBUILT_OS "osx")
    endif()
endif()

set(NATIVEAOT_MINT_PREBUILT_PATH "${NATIVEAOT_MINT_PREBUILT_OS}-${CLR_CMAKE_TARGET_ARCH}")
message(WARNING "NATIVEAOT_MINT_PREBUILT_PATH: ${NATIVEAOT_MINT_PREBUILT_PATH}")

add_subdirectory(${CLR_SRC_NATIVE_DIR}/../mono/mono/eglib ${CMAKE_CURRENT_BINARY_DIR}/eglib)

add_library(mint STATIC $<TARGET_OBJECTS:eglib_objects> ${MINT_SOURCES})
target_link_libraries(mint PRIVATE eglib_api)
target_include_directories(mint BEFORE PRIVATE ${NATIVEAOT_MINT_INC_PATH} ${NATIVEAOT_MINT_INC_PATH}/prebuilt/${NATIVEAOT_MINT_PREBUILT_PATH})
target_compile_definitions(mint PRIVATE -DNATIVEAOT_MINT)

install_static_library(mint aotsdk nativeaot)
