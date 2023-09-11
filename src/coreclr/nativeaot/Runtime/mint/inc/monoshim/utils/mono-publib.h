#ifndef _MONOSHIM_UTILS_MONO_PUBLIB_H
#define _MONOSHIM_UTILS_MONO_PUBLIB_H

#ifdef __cplusplus
#define MONO_BEGIN_DECLS extern "C" {
#define MONO_END_DECLS }
#else
#define MONO_BEGIN_DECLS
#define MONO_END_DECLS
#endif

/* we're doing static linking, no need for public API symbols */
#define MONO_API /* empty */

#endif /*_MONOSHIM_UTILS_MONO_PUBLIB_H*/
