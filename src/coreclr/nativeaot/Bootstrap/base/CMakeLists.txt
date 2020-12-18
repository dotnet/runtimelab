project(bootstrapper)

set(SOURCES
    ../main.cpp
)

add_library(bootstrapper STATIC ${SOURCES})

# Install the static bootstrapper library
install (TARGETS bootstrapper DESTINATION aotsdk)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/bootstrapper.dir/$<CONFIG>/bootstrapper.pdb DESTINATION aotsdk)
endif()
