#ifndef _MONOSHIM_METADATA_MONO_BASIC_BLOCK_H
#define _MONOSHIM_METADATA_MONO_BASIC_BLOCK_H

#include <glib.h>

typedef struct _MonoSimpleBasicBlock MonoSimpleBasicBlock;

struct _MonoSimpleBasicBlock {
	MonoSimpleBasicBlock *next, *left, *right, *parent;
	GSList *out_bb;
	int start, end;
	unsigned colour   : 1;
	unsigned dead     : 1;
};


#endif /*_MONOSHIM_METADATA_MONO_BASIC_BLOCK_H*/
