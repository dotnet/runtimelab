if(WIN32)
  add_definitions(-DUNICODE=1)
endif (WIN32)

if(MSVC)
  add_compile_options(/EHa- /EHs)   # Native AOT runtime does not use C++ exception handling

  add_compile_options(/guard:cf-)   # CFG runtime checks in C++ code are unnecessary overhead unless Native AOT compiler produces CFG compliant code as well
                                    # and CFG is enabled in the linker
endif (MSVC)

add_subdirectory(Bootstrap)
add_subdirectory(Runtime)
