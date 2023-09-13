#ifndef _MINT_TRANSFORM_H
#define _MINT_TRANSFORM_H

#include <monoshim/utils/mono-error-internals.h>

typedef struct InterpMethod InterpMethod;
typedef struct _ThreadContext ThreadContext;

void
mono_interp_transform_method (InterpMethod *imethod, ThreadContext *context, MonoError *error);



#endif/*_MINT_TRANSFORM_H*/
