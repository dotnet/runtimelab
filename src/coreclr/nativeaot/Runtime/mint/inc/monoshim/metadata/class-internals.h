#ifndef _MONOSHIM_METADATA_CLASS_INTERNALS_H
#define _MONOSHIM_METADATA_CLASS_INTERNALS_H

typedef struct _MonoMethod MonoMethod;

typedef enum {
    MONO_WRAPPER_NONE = 0,
    MONO_WRAPPER_SYNCHRONIZED,
    MONO_WRAPPER_DYNAMIC_METHOD,

    MONO_WRAPPER_NUM
} MonoWrapperType;

void*
m_method_alloc0 (MonoMethod *method, guint size);


#endif
