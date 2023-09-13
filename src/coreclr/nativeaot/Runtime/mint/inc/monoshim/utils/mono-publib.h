#ifndef _MONOSHIM_UTILS_MONO_PUBLIB_H
#define _MONOSHIM_UTILS_MONO_PUBLIB_H

#ifdef __cplusplus
#define MONO_BEGIN_DECLS extern "C" {
#define MONO_END_DECLS }
#else
#define MONO_BEGIN_DECLS
#define MONO_END_DECLS
#endif

typedef int32_t		mono_bool;
typedef uint8_t		mono_byte;
typedef mono_byte       MonoBoolean;

/* we're doing static linking, no need for public API symbols */
#define MONO_API /* empty */

#endif /*_MONOSHIM_UTILS_MONO_PUBLIB_H*/
