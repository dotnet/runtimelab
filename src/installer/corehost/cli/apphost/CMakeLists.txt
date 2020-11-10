# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

if(NOT RUNTIME_FLAVOR STREQUAL "Mono")
  # FIXME: The static build currently doesn't work with the Mono runtime.
  add_subdirectory(static)
endif()
add_subdirectory(standalone)
