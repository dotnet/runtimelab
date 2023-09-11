project(mint C)

set(NATIVEAOT_MINT 1)
set(NATIVEAOT_MINT_INC_PATH ${CMAKE_CURRENT_SOURCE_DIR}/inc)

set(SHARED_CONTAINERS_SOURCE_PATH "${CLR_SRC_NATIVE_DIR}/containers")
set(SHARED_MINIPAL_SOURCE_PATH "${CLR_SRC_NATIVE_DIR}/minipal")
set(MONO_BASEDIR_PATH ${CLR_SRC_NATIVE_DIR}/../mono/mono)
set(MONO_INTERP_SOURCES_PATH "${MONO_BASEDIR_PATH}/mini/interp")

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_EXTENSIONS ON)

# "${MONO_INTERP_SOURCES_PATH}/transform.c"
set(MINT_SOURCES
    mint.c)

add_subdirectory(${CLR_SRC_NATIVE_DIR}/../mono/mono/eglib ${CMAKE_CURRENT_BINARY_DIR}/eglib)

add_library(mint STATIC ${MINT_SOURCES})
target_sources(mint PRIVATE $<TARGET_OBJECTS:eglib_objects>)
target_link_libraries(mint PRIVATE eglib_api)
target_include_directories(mint BEFORE PRIVATE ${NATIVEAOT_MINT_INC_PATH} ${NATIVEAOT_MINT_INC_PATH}/prebuilt/osx-arm64)
target_compile_definitions(mint PRIVATE -DNATIVEAOT_MINT)

install_static_library(mint aotsdk nativeaot)
