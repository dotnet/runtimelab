project(bootstrapper)

set(SOURCES
    ../main.cpp
)

add_library(bootstrapper STATIC ${SOURCES})

install_static_library(bootstrapper aotsdk)
