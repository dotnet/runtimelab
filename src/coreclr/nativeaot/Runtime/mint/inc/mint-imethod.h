#ifndef _MINT_IMETHOD_H
#define _MINT_IMETHOD_H

#include <monoshim/missing-symbols.h>

typedef struct InterpMethod InterpMethod;

InterpMethod * mono_interp_get_imethod (MonoMethod *method);

void
mint_interp_imethod_dump_code (InterpMethod *imethod);

#endif/*_MINT_IMETHOD_H*/
