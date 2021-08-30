/**
 * \file
 */

#ifndef _MONO_UTILS_RAND_H_
#define _MONO_UTILS_RAND_H_

#include <glib.h>

#include "mono-compiler.h"
#include "mono-error.h"

MONO_COMPONENT_API
gboolean
mono_rand_open (void);

MONO_COMPONENT_API
gpointer
mono_rand_init (const guchar *seed, gssize seed_size);

MONO_COMPONENT_API
gboolean
mono_rand_try_get_bytes (gpointer *handle, guchar *buffer, gssize buffer_size, MonoError *error);

gboolean
mono_rand_try_get_uint32 (gpointer *handle, guint32 *val, guint32 min, guint32 max, MonoError *error);

MONO_COMPONENT_API
void
mono_rand_close (gpointer handle);

#endif /* _MONO_UTILS_RAND_H_ */
