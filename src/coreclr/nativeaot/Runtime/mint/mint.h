#ifndef _MINT_H
#define _MINT_H

#include <monoshim/missing-symbols.h>
#include <mint-transform.h>
#include <mint-abstraction-nativeaot.h>
#include <mint-ee-abstraction-nativeaot.h>


void
mint_entrypoint(MintAbstractionNativeAot *itf, MintEEAbstractionNativeAot* eeItf);

InterpMethod*
mint_testing_transform_sample(MonoMethod *monoMethodPtr);

#endif/*_MINT_H*/
