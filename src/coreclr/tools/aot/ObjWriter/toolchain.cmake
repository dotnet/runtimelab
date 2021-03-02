if (WIN32)
  # Enable debug information for Release builds
  set(CMAKE_CXX_FLAGS_RELEASE_INIT /Zi)
  set(CMAKE_C_FLAGS_RELEASE_INIT /Zi)
  set(CMAKE_SHARED_LINKER_FLAGS_RELEASE_INIT "/debug /OPT:REF /OPT:ICF")
endif()
