#ifndef _MONOSHIM_UTILS_MONO_COMPILER_H
#define _MONOSHIM_UTILS_MONO_COMPILER_H

#define MONO_NEVER_INLINE __attribute__((noinline))
#define MONO_ALWAYS_INLINE __attribute__((always_inline))

#if defined (__clang__)
#define MONO_NO_OPTIMIZATION __attribute__ ((optnone))
#elif __GNUC__ > 4 || (__GNUC__ == 4 && __GNUC_MINOR__ >= 4)
#define MONO_NO_OPTIMIZATION __attribute__ ((optimize("O0")))
#else
#define MONO_NO_OPTIMIZATION /*empty*/
#endif

#ifdef _MSC_VER
#define MONO_PRAGMA_WARNING_PUSH() __pragma(warning (push))
#define MONO_PRAGMA_WARNING_DISABLE(x) __pragma(warning (disable:x))
#define MONO_PRAGMA_WARNING_POP() __pragma(warning (pop))

#define MONO_DISABLE_WARNING(x) \
		MONO_PRAGMA_WARNING_PUSH() \
		MONO_PRAGMA_WARNING_DISABLE(x)

#define MONO_RESTORE_WARNING \
		MONO_PRAGMA_WARNING_POP()
#else
#define MONO_PRAGMA_WARNING_PUSH()
#define MONO_PRAGMA_WARNING_DISABLE(x)
#define MONO_PRAGMA_WARNING_POP()
#define MONO_DISABLE_WARNING(x)
#define MONO_RESTORE_WARNING
#endif


#endif /*_MONOSHIM_UTILS_MONO_COMPILER_H*/
