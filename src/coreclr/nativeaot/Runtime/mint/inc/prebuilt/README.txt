The files in this directory are copied from a build of Mono:
artifacts/obj/mono/<triple>/config.h
and
artifacts/obj/mono/<triple>/mono/eglib-config.h

The "correct" thing to do is to share mono's "autoconf" CMake logic
(src/mono/cmake/configure.cmake), but in the interest of expediency
we just copy the headers for the time being.
