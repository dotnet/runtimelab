#include <stdatomic.h>
#include <config.h>
#include <glib.h>

#include <monoshim/missing-symbols.h>
#include <mint-abstraction-nativeaot.h>

#include "mint-itf.h"

void
mono_metadata_free_mh (MonoMethodHeader *header) {
    // no-op
    // FIXME: this might dispose of somethign in managed,
    // but we should be careful that we're refcounting if we're giving the same
    // header to multiple callers
 }


gpointer
mint_imethod_alloc0 (TransformData *td, size_t size)
{
    // FIXME: this shouldn't go here. instead we should do like the mono version:
    //
    // if (td->rtm->method->dynamic)
    //   return mono_dyn_method_alloc0 (td->rtm->method, (guint)size);
    // else
    //   return mono_mem_manager_alloc0 (td->mem_manager, (guint)size);
    //
    // in particular we should have a memory manager tied to the InterpMethod that
    // is bound to the lifetime of the dynamic method delegate

    return g_malloc0 (size);
}

static MintAbstractionNativeAot * mint_itf_singleton;

MintAbstractionNativeAot *mint_itf(void) {
    return mint_itf_singleton;
}

void
mint_itf_initialize(MintAbstractionNativeAot* newitf)
{
    // TODO: these should all be set from managed

    newitf->imethod_alloc0 = &mint_imethod_alloc0;

    mint_itf_singleton = newitf;

}
