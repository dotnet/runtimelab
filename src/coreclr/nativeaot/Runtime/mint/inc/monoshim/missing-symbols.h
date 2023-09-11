#ifndef _MONOSHIM_MISSING_SYMBOLS_H
#define _MONOSHIM_MISSING_SYMBOLS_H

#include <monoshim/utils/mono-compiler.h>
#include <monoshim/utils/mono-publib.h>
#include <monoshim/utils/mono-memory-model.h>

typedef struct _MonoBitSet MonoBitSet;

#include <monoshim/metadata/opcodes-types.h>
#include <monoshim/metadata/profiler-types.h>
#include <monoshim/metadata/tabledefs.h>
#include <monoshim/metadata/class-internals.h>

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

typedef struct _MonoClassField MonoClassField;

typedef struct _MonoGenericContext MonoGenericContext;

typedef struct _MonoJitICallInfo MonoJitICallInfo;

typedef int MonoJitICallId;

typedef struct _MonoVTable MonoVTable;

/* metadata algorithms */

typedef struct _MonoSimpleBasicBlock MonoSimpleBasicBlock;


/* mini runtime */

typedef struct _SeqPoint SeqPoint;
typedef struct _MonoJitInfo MonoJitInfo;

/* runtime managed heap objects! */

typedef struct _MonoString MonoString;

#endif/*_MONOSHIM_MISSING_SYMBOLS_H*/
