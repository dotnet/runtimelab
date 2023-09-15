#ifndef _MONOSHIM_UTILS_MONO_THREADS_API_H
#define _MONOSHIM_UTILS_MONO_THREADS_API_H

#define MONO_ENTER_GC_UNSAFE do {
#define MONO_EXIT_GC_UNSAFE (void)0; } while (0)

#define MONO_ENTER_GC_SAFE	do {
#define MONO_EXIT_GC_SAFE	(void)0; } while (0)

#define MONO_REQ_GC_UNSAFE_MODE do {} while(0)


#endif /*_MONOSHIM_UTILS_MONO_THREADS_API_H*/
