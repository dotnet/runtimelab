#ifndef _MONOSHIM_MISSING_SYMBOLS_H
#define _MONOSHIM_MISSING_SYMBOLS_H

#include <monoshim/utils/mono-compiler.h>
#include <monoshim/utils/mono-publib.h>
#include <monoshim/utils/mono-memory-model.h>

typedef struct _MonoBitSet MonoBitSet;

#include <monoshim/metadata/blob.h>
#include <monoshim/metadata/opcodes-types.h>
#include <monoshim/metadata/profiler-types.h>
#include <monoshim/metadata/metadata-types.h>
#include <monoshim/metadata/tabledefs.h>
#include <monoshim/metadata/class-internals.h>

#include <monoshim/metadata/mono-basic-block.h>

// mini.h does this in Mono

/*
 * Pull the list of opcodes
 */
#define OPDEF(a,b,c,d,e,f,g,h,i,j) \
	a = i,

enum {
#include "monoshim/cil/opcode.def"
	CEE_LASTOP
};
#undef OPDEF


typedef struct _MonoError MonoError;

typedef struct _MonoImage MonoImage;

typedef struct _MonoType MonoType;
typedef struct _MonoClass MonoClass;

typedef struct _MonoMethod MonoMethod;
typedef struct _MonoMethodSignature MonoMethodSignature;
typedef struct _MonoMethodHeader MonoMethodHeader;

typedef struct _MonoExceptionClause MonoExceptionClause;

typedef struct _MonoClassField MonoClassField;

typedef struct _MonoGenericContext MonoGenericContext;
typedef struct _MonoGenericContainer MonoGenericContainer;

typedef struct _MonoJitICallInfo MonoJitICallInfo;

typedef int MonoJitICallId;

typedef struct _MonoVTable MonoVTable;

/* metadata algorithms */

typedef struct _MonoSimpleBasicBlock MonoSimpleBasicBlock;


/* debug support */
typedef struct _MonoDebugLineNumberEntry	MonoDebugLineNumberEntry;
struct _MonoDebugLineNumberEntry {
	uint32_t il_offset;
	uint32_t native_offset;
};


/* mini runtime */

typedef struct _SeqPoint SeqPoint;
typedef struct _MonoJitInfo MonoJitInfo;

typedef struct _MonoJitMemoryManager MonoJitMemoryManager;

/* runtime managed heap objects! */

typedef struct _MonoString MonoString;


/* mini.h */
#define MONO_TIME_TRACK(cost_center, expr) expr

#define MONO_PROFILER_RAISE(name,args) /* empty */

static gpointer mono_jit_trace_calls = NULL; // FIXME: hack!

static gpointer mono_stats_method_desc = NULL; // FIXME: hack!

#endif/*_MONOSHIM_MISSING_SYMBOLS_H*/
