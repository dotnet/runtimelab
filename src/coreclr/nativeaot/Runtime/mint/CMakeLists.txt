project(mint C)

set(SHARED_CONTAINERS_SOURCE_PATH "${CLR_SRC_NATIVE_DIR}/containers")
set(SHARED_MINIPAL_SOURCE_PATH "${CLR_SRC_NATIVE_DIR}/minipal")
set(MONO_BASEDIR_PATH ${CLR_SRC_NATIVE_DIR}/../mono/mono)
set(MONO_INTERP_SOURCES_PATH "${MONO_BASEDIR_PATH}/mini/interp")

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_EXTENSIONS ON)

# "${MONO_INTERP_SOURCES_PATH}/transform.c"
set(MINT_SOURCES
    mint.c)

add_library(mint STATIC ${MINT_SOURCES})
target_include_directories(mint PRIVATE inc)
target_compile_definitions(mint PRIVATE -DNATIVEAOT_MINT)

install_static_library(mint aotsdk nativeaot)
