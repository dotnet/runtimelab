project(Runtime)

# Full version of the runtime is required by the JIT CodeGen.
# The low-level helpers can be implemented in assembly code.

# Include auto-generated files on include path
set(CMAKE_INCLUDE_CURRENT_DIR ON)

add_definitions(-DFEATURE_RX_THUNKS)

# For Windows ARM/ARM64 .asm files in this list are replaced with the corresponding .obj files
set(RUNTIME_SOURCES_ARCH_ASM_RESOLVED "${RUNTIME_SOURCES_ARCH_ASM}")

if (CLR_CMAKE_TARGET_WIN32)
  if (CLR_CMAKE_HOST_ARCH_ARM OR CLR_CMAKE_HOST_ARCH_ARM64)
    # Needed to include AsmOffsets.inc
    include_directories(BEFORE ${CMAKE_CURRENT_BINARY_DIR})
    preprocess_files(RUNTIME_SOURCES_ARCH_ASM ${RUNTIME_SOURCES_ARCH_ASM})
    set(RUNTIME_SOURCES_ARCH_ASM_RESOLVED "${RUNTIME_SOURCES_ARCH_ASM}")
    if (CMAKE_GENERATOR MATCHES "Visual Studio")
      compile_asm(TARGET Runtime ASM_FILES ${RUNTIME_SOURCES_ARCH_ASM} OUTPUT_OBJECTS RUNTIME_SOURCES_ARCH_ASM_RESOLVED)
    endif()
  endif()
endif (CLR_CMAKE_TARGET_WIN32)

add_library(Runtime STATIC ${COMMON_RUNTIME_SOURCES} ${FULL_RUNTIME_SOURCES} ${RUNTIME_SOURCES_ARCH_ASM_RESOLVED})

add_library(Runtime.ServerGC STATIC ${COMMON_RUNTIME_SOURCES} ${FULL_RUNTIME_SOURCES} ${RUNTIME_SOURCES_ARCH_ASM_RESOLVED} ${SERVER_GC_SOURCES})

target_compile_definitions(Runtime.ServerGC PRIVATE -DFEATURE_SVR_GC)


# Get the current list of definitions
get_compile_definitions(DEFINITIONS)

set(ASM_OFFSETS_CSPP ${RUNTIME_DIR}/../Runtime.Base/src/AsmOffsets.cspp)

if(WIN32)
    set(COMPILER_LANGUAGE "")
    set(PREPROCESSOR_FLAGS -EP)
    set(ASM_OFFSETS_CPP ${RUNTIME_DIR}/windows/AsmOffsets.cpp)
else()
    set(COMPILER_LANGUAGE -x c++)
    set(PREPROCESSOR_FLAGS -E -P)
    set(ASM_OFFSETS_CPP ${RUNTIME_DIR}/unix/AsmOffsets.cpp)
endif()

add_custom_command(
    # The AsmOffsets.cs is consumed later by the managed build
    TARGET Runtime
    COMMAND ${CMAKE_CXX_COMPILER} ${COMPILER_LANGUAGE} ${DEFINITIONS} ${PREPROCESSOR_FLAGS} -I"${ARCH_SOURCES_DIR}" "${ASM_OFFSETS_CSPP}" >"${CMAKE_CURRENT_BINARY_DIR}/AsmOffsets.cs"
    DEPENDS "${RUNTIME_DIR}/AsmOffsets.cpp" "${RUNTIME_DIR}/AsmOffsets.h"
)

add_custom_command(
    COMMAND ${CMAKE_CXX_COMPILER} ${DEFINITIONS} ${PREPROCESSOR_FLAGS} -I"${RUNTIME_DIR}" -I"${ARCH_SOURCES_DIR}" "${ASM_OFFSETS_CPP}" >"${CMAKE_CURRENT_BINARY_DIR}/AsmOffsets.inc"
    OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/AsmOffsets.inc"
    DEPENDS "${ASM_OFFSETS_CPP}" "${RUNTIME_DIR}/AsmOffsets.h"
    COMMENT "Generating AsmOffsets.inc"
)

set_source_files_properties("${CMAKE_CURRENT_BINARY_DIR}/AsmOffsets.inc" PROPERTIES GENERATED TRUE)

# Runtime and Runtime.ServerGC share AsmOffsets.inc and assembler helpers (for Windows ARM/ARM64).
# Avoid race condition during a build using a separate target.
add_custom_target(
  RuntimeAsmHelpers
  DEPENDS "${CMAKE_CURRENT_BINARY_DIR}/AsmOffsets.inc" "${RUNTIME_SOURCES_ARCH_ASM_RESOLVED}"
)

add_dependencies(Runtime RuntimeAsmHelpers)
add_dependencies(Runtime.ServerGC RuntimeAsmHelpers)

# REVIEW: COMPILE_DEFINITIONS_LIST is not defined here. Should we remove the following code?

# Extract the definitions for the ASM code. Since there is a bug in the cmake that prevents us from
# using the generator expressions, we split the definitions into lists based on the configuration.
foreach(DEFINITION IN LISTS COMPILE_DEFINITIONS_LIST)
    if (${DEFINITION} MATCHES "^\\$<\\$<CONFIG:([^>]+)>:([^>]+)>$")
        # The entry contains generator expression, so insert the definition into a definitions list
        # corresponding to the config
       string(TOUPPER ${CMAKE_MATCH_1} CONFIG)
       set(ASM_DEFINITIONS_${CONFIG} ${ASM_DEFINITIONS_${CONFIG}} ${CMAKE_MATCH_2})
    else()
        list(APPEND ASM_DEFINITIONS ${DEFINITION})
    endif()
endforeach()

# Add defines for the ASM. Unfortunately setting it on the target is ignored for asm by the cmake, so we have
# to set it on the sources.
set_property(SOURCE ${RUNTIME_SOURCES_ARCH_ASM} PROPERTY COMPILE_DEFINITIONS ${ASM_DEFINITIONS})
foreach(CONFIG IN LISTS CMAKE_CONFIGURATION_TYPES)
    string(TOUPPER ${CONFIG} CONFIG)
    set_property(SOURCE ${RUNTIME_SOURCES_ARCH_ASM} PROPERTY COMPILE_DEFINITIONS_${CONFIG} ${ASM_DEFINITIONS_${CONFIG}})
endforeach()

# Install the static Runtime library
install (TARGETS Runtime Runtime.ServerGC DESTINATION aotsdk)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/Runtime.dir/$<CONFIG>/Runtime.pdb DESTINATION aotsdk)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/Runtime.ServerGC.dir/$<CONFIG>/Runtime.ServerGC.pdb DESTINATION aotsdk)
endif()
