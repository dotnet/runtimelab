#include <config.h>
#include <glib.h>

#include <monoshim/missing-symbols.h>
#include <monoshim/missing-symbols-ee.h>

#include <mint-ee-abstraction-nativeaot.h>

#include "mint-itf.h"

static MintEEAbstractionNativeAot * mint_ee_itf_singleton;

MintEEAbstractionNativeAot *mint_ee_itf(void) {
    return mint_ee_itf_singleton;
}

void
mint_ee_itf_initialize(MintEEAbstractionNativeAot* newitf)
{
    mint_ee_itf_singleton = newitf;
    mono_ee_interp_init (NULL);
}
