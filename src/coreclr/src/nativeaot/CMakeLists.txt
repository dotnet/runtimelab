if(WIN32)
    add_definitions(-DUNICODE=1)
endif (WIN32)

add_subdirectory(Bootstrap)
add_subdirectory(Runtime)
