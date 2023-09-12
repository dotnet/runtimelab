#ifndef _MONOSHIM_UTILS_MONO_ERROR_INTERNAL_H
#define _MONOSHIM_UTILS_MONO_ERROR_INTERNAL_H

typedef struct _MonoError MonoError;

#define ERROR_DECL(name) MonoError *name = NULL;

#define goto_if_nok(error,label) do { } while (0)
#define return_if_nok(error) do { } while (0)

static inline void error_init (MonoError *error) { }

#endif /*_MONOSHIM_UTILS_MONO_ERROR_INTERNAL_H*/
