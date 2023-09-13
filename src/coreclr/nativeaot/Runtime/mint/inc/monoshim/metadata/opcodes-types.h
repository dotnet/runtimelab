#ifndef _MONOSHIM_METADATA_OPCODES_TYPE_H
#define _MONOSHIM_METADATA_OPCODES_TYPE_H

#include <stdint.h>
#include <monoshim/utils/mono-publib.h>

MONO_BEGIN_DECLS

#define MONO_CUSTOM_PREFIX 0xf0

#define OPDEF(a,b,c,d,e,f,g,h,i,j) \
	MONO_ ## a,

typedef enum MonoOpcodeEnum {
	MonoOpcodeEnum_Invalid = -1,
#include "monoshim/cil/opcode.def"
	MONO_CEE_LAST
} MonoOpcodeEnum;

#undef OPDEF

enum {
	MONO_FLOW_NEXT,
	MONO_FLOW_BRANCH,
	MONO_FLOW_COND_BRANCH,
	MONO_FLOW_ERROR,
	MONO_FLOW_CALL,
	MONO_FLOW_RETURN,
	MONO_FLOW_META
};

enum {
	MonoInlineNone          = 0,
	MonoInlineType          = 1,
	MonoInlineField         = 2,
	MonoInlineMethod        = 3,
	MonoInlineTok           = 4,
	MonoInlineString        = 5,
	MonoInlineSig           = 6,
	MonoInlineVar           = 7,
	MonoShortInlineVar      = 8,
	MonoInlineBrTarget      = 9,
	MonoShortInlineBrTarget = 10,
	MonoInlineSwitch        = 11,
	MonoInlineR             = 12,
	MonoShortInlineR        = 13,
	MonoInlineI             = 14,
	MonoShortInlineI        = 15,
	MonoInlineI8            = 16,
};

typedef struct {
	unsigned char argument;
	unsigned char flow_type;
	unsigned short opval;
} MonoOpcode;

extern const MonoOpcode mono_opcodes [];

const char* mono_opcode_name (int opcode);

MonoOpcodeEnum mono_opcode_value (const uint8_t **ip, const uint8_t *end);

MONO_END_DECLS


#endif /*_MONOSHIM_METADATA_OPCODES_TYPE_H*/
