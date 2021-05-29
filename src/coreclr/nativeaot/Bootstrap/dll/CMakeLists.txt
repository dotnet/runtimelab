project(bootstrapperdll)

add_definitions(-DCORERT_DLL)

set(SOURCES
    ../main.cpp
)

add_library(bootstrapperdll STATIC ${SOURCES})

install_static_library(bootstrapperdll aotsdk)
