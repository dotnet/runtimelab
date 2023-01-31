//
// mini-llvm-cpp.cpp: C++ support classes for the mono LLVM integration
//
// (C) 2009-2011 Novell, Inc.
// Copyright 2011 Xamarin, Inc (http://www.xamarin.com)
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// We need to override some stuff in LLVM, but this cannot be done using the C
// interface, so we have to use some C++ code here.
// The things which we override are:
// - the default JIT code manager used by LLVM doesn't allocate memory using
//   MAP_32BIT, we require it.
// - add some callbacks so we can obtain the size of methods and their exception
//   tables.
//

//
// Mono's internal header files are not C++ clean, so avoid including them if 
// possible
//

#include "config.h"

#include <stdint.h>

#ifdef _MSC_VER
// Disable warnings generated by LLVM headers.
#pragma warning(disable:4141) // modifier' : used more than once
#pragma warning(disable:4146) // unary minus operator applied to unsigned type, result still unsigned
#pragma warning(disable:4267) // conversion from 'size_t' to 'unsigned int', possible loss of data
#pragma warning(disable:4310) // cast truncates constant value
#pragma warning(disable:4324) // structure was padded due to alignment specifier
#pragma warning(disable:4458) // declaration of '' hides class member
#pragma warning(disable:4624) // destructor was implicitly defined as deleted
#pragma warning(disable:4800) // type' : forcing value to bool 'true' or 'false' (performance warning)
#endif

#include <llvm/Support/raw_ostream.h>
#include <llvm/IR/Function.h>
#include <llvm/IR/IRBuilder.h>
#include <llvm/IR/Module.h>
#include <llvm/IR/DIBuilder.h>
#include <llvm/IR/MDBuilder.h>

#include <llvm/IR/InstrTypes.h> // CallBase
#include <llvm/Support/Host.h> // llvm::sys::getHostCPUFeatures
#include <llvm/Analysis/TargetTransformInfo.h> // Intrinsic::ID
#include <llvm/IR/IntrinsicsX86.h>
#include <llvm/IR/IntrinsicsAArch64.h>
#include <llvm/IR/IntrinsicsWebAssembly.h>

#include "mini-llvm-cpp.h"

using namespace llvm;

#define Acquire AtomicOrdering::Acquire
#define Release AtomicOrdering::Release
#define SequentiallyConsistent AtomicOrdering::SequentiallyConsistent

void
mono_llvm_dump_value (LLVMValueRef value)
{
	/* Same as LLVMDumpValue (), but print to stdout */
	fflush (stdout);
	outs () << (*unwrap<Value> (value)) << "\n";
	outs ().flush ();
}

void
mono_llvm_dump_module (LLVMModuleRef module)
{
	/* Same as LLVMDumpModule (), but print to stdout */
	fflush (stdout);
	outs () << (*unwrap (module)) << "\n";
	outs ().flush ();
}

void
mono_llvm_dump_type (LLVMTypeRef type)
{
	fflush (stdout);
	outs () << (*unwrap (type)) << "\n";
	outs ().flush ();
}

static inline llvm::Align
to_align (int alignment)
{
	return llvm::Align (alignment);
}

/* Missing overload for building an alloca with an alignment */
LLVMValueRef
mono_llvm_build_alloca (LLVMBuilderRef builder, LLVMTypeRef Ty, LLVMValueRef ArraySize, int alignment, const char *Name)
{
	auto ty = unwrap (Ty);
	auto sz = unwrap (ArraySize);
	auto b = unwrap (builder);
	auto ins = alignment > 0
		? b->Insert (new AllocaInst (ty, 0, sz, to_align (alignment)), Name)
		: b->CreateAlloca (ty, 0, sz, Name);
	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_atomic_load (LLVMBuilderRef builder, LLVMTypeRef Type, LLVMValueRef PointerVal,
							 const char *Name, gboolean is_volatile, int alignment, BarrierKind barrier)
{
	LoadInst *ins = unwrap(builder)->CreateLoad(unwrap(Type), unwrap(PointerVal), is_volatile, Name);

	ins->setAlignment (to_align (alignment));
	switch (barrier) {
	case LLVM_BARRIER_NONE:
		break;
	case LLVM_BARRIER_ACQ:
		ins->setOrdering(Acquire);
		break;
	case LLVM_BARRIER_SEQ:
		ins->setOrdering(SequentiallyConsistent);
		break;
	default:
		g_assert_not_reached ();
		break;
	}

	return wrap(ins);
}

LLVMValueRef 
mono_llvm_build_aligned_load (LLVMBuilderRef builder, LLVMTypeRef Type, LLVMValueRef PointerVal,
							  const char *Name, gboolean is_volatile, int alignment)
{
	LoadInst *ins;

	ins = unwrap(builder)->CreateLoad(unwrap(Type), unwrap(PointerVal), is_volatile, Name);
	ins->setAlignment (to_align (alignment));

	return wrap(ins);
}

LLVMValueRef 
mono_llvm_build_aligned_store (LLVMBuilderRef builder, LLVMValueRef Val, LLVMValueRef PointerVal,
							   gboolean is_volatile, int alignment)
{
	StoreInst *ins;

	ins = unwrap(builder)->CreateStore(unwrap(Val), unwrap(PointerVal), is_volatile);
	ins->setAlignment (to_align (alignment));

	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_atomic_store (LLVMBuilderRef builder, LLVMValueRef Val, LLVMValueRef PointerVal,
							  BarrierKind barrier, int alignment)
{
	StoreInst *ins;

	ins = unwrap(builder)->CreateStore(unwrap(Val), unwrap(PointerVal), false);
	ins->setAlignment (to_align (alignment));

	switch (barrier) {
	case LLVM_BARRIER_NONE:
		break;
	case LLVM_BARRIER_REL:
		ins->setOrdering(Release);
		break;
	case LLVM_BARRIER_SEQ:
		ins->setOrdering(SequentiallyConsistent);
		break;
	default:
		g_assert_not_reached ();
		break;
	}

	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_cmpxchg (LLVMBuilderRef builder, LLVMValueRef ptr, LLVMValueRef cmp, LLVMValueRef val)
{
	AtomicCmpXchgInst *ins;

#if LLVM_API_VERSION >= 1300
	ins = unwrap(builder)->CreateAtomicCmpXchg (unwrap(ptr), unwrap (cmp), unwrap (val), MaybeAlign (), SequentiallyConsistent, SequentiallyConsistent);
#else
	ins = unwrap(builder)->CreateAtomicCmpXchg (unwrap(ptr), unwrap (cmp), unwrap (val), SequentiallyConsistent, SequentiallyConsistent);
#endif
	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_atomic_rmw (LLVMBuilderRef builder, AtomicRMWOp op, LLVMValueRef ptr, LLVMValueRef val)
{
	AtomicRMWInst::BinOp aop = AtomicRMWInst::Xchg;
	AtomicRMWInst *ins;

	switch (op) {
	case LLVM_ATOMICRMW_OP_XCHG:
		aop = AtomicRMWInst::Xchg;
		break;
	case LLVM_ATOMICRMW_OP_ADD:
		aop = AtomicRMWInst::Add;
		break;
	case LLVM_ATOMICRMW_OP_AND:
		aop = AtomicRMWInst::And;
		break;
	case LLVM_ATOMICRMW_OP_OR:
		aop = AtomicRMWInst::Or;
		break;
	default:
		g_assert_not_reached ();
		break;
	}

#if LLVM_API_VERSION >= 1300
	ins = unwrap (builder)->CreateAtomicRMW (aop, unwrap (ptr), unwrap (val), MaybeAlign (), SequentiallyConsistent);
#else
	ins = unwrap (builder)->CreateAtomicRMW (aop, unwrap (ptr), unwrap (val), SequentiallyConsistent);
#endif
	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_fence (LLVMBuilderRef builder, BarrierKind kind)
{
	FenceInst *ins;
	AtomicOrdering ordering;

	g_assert (kind != LLVM_BARRIER_NONE);

	switch (kind) {
	case LLVM_BARRIER_ACQ:
		ordering = Acquire;
		break;
	case LLVM_BARRIER_REL:
		ordering = Release;
		break;
	case LLVM_BARRIER_SEQ:
		ordering = SequentiallyConsistent;
		break;
	default:
		g_assert_not_reached ();
		break;
	}

	ins = unwrap (builder)->CreateFence (ordering);
	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_weighted_branch (LLVMBuilderRef builder, LLVMValueRef cond, LLVMBasicBlockRef t, LLVMBasicBlockRef f, uint32_t t_weight, uint32_t f_weight)
{
	auto b = unwrap (builder);
	auto &ctx = b->getContext ();
	MDBuilder mdb{ctx};
	auto weights = mdb.createBranchWeights (t_weight, f_weight);
	auto ins = b->CreateCondBr (unwrap (cond), unwrap (t), unwrap (f), weights);
	return wrap (ins);
}

LLVMValueRef
mono_llvm_build_exact_ashr (LLVMBuilderRef builder, LLVMValueRef lhs, LLVMValueRef rhs) {
	auto b = unwrap (builder);
	auto ins = b->CreateAShr (unwrap (lhs), unwrap (rhs), "", true);
	return wrap (ins);
}

void
mono_llvm_add_string_metadata (LLVMValueRef insref, const char* label, const char* text)
{
	auto ins = unwrap<Instruction> (insref);
	auto &ctx = ins->getContext ();
	ins->setMetadata (label, MDNode::get (ctx, MDString::get (ctx, text)));
}

void
mono_llvm_set_implicit_branch (LLVMBuilderRef builder, LLVMValueRef branch)
{
	auto b = unwrap (builder);
	auto &ctx = b->getContext ();
	auto ins = unwrap<Instruction> (branch);
	ins->setMetadata (LLVMContext::MD_make_implicit, MDNode::get (ctx, {}));
}

void
mono_llvm_set_must_tailcall (LLVMValueRef call_ins)
{
	CallInst *ins = (CallInst*)unwrap (call_ins);

	ins->setTailCallKind (CallInst::TailCallKind::TCK_MustTail);
}

void
mono_llvm_replace_uses_of (LLVMValueRef var, LLVMValueRef v)
{
	Value *V = ConstantExpr::getTruncOrBitCast (unwrap<Constant> (v), unwrap (var)->getType ());
	unwrap (var)->replaceAllUsesWith (V);
}

LLVMValueRef
mono_llvm_create_constant_data_array (const uint8_t *data, int len)
{
	return wrap(ConstantDataArray::get (*unwrap(LLVMGetGlobalContext ()), makeArrayRef(data, len)));
}

void
mono_llvm_set_is_constant (LLVMValueRef global_var)
{
	unwrap<GlobalVariable>(global_var)->setConstant (true);
}

void
mono_llvm_set_call_nonnull_arg (LLVMValueRef wrapped_calli, int argNo)
{
	Instruction *calli = unwrap<Instruction> (wrapped_calli);

	dyn_cast<CallBase>(calli)->addParamAttr (argNo, Attribute::NonNull);
}

static void
add_ret_attr (LLVMValueRef wrapped_calli, Attribute::AttrKind Kind)
{
	Instruction *calli = unwrap<Instruction> (wrapped_calli);

#if LLVM_API_VERSION >= 1400
	dyn_cast<CallBase>(calli)->addRetAttr (Kind);
#else
	dyn_cast<CallBase>(calli)->addAttribute (AttributeList::ReturnIndex, Kind);
#endif
}

void
mono_llvm_set_call_nonnull_ret (LLVMValueRef wrapped_calli)
{
	add_ret_attr (wrapped_calli, Attribute::NonNull);
}

void
mono_llvm_set_func_nonnull_arg (LLVMValueRef func, int argNo)
{
	unwrap<Function>(func)->addParamAttr (argNo, Attribute::NonNull);
}

gboolean
mono_llvm_can_be_gep (LLVMValueRef base, LLVMValueRef* gep_base, LLVMValueRef* gep_offset)
{
	// Look for a pattern like this:
	//   %1 = ptrtoint i8* %gep_base to i64
	//   %2 = add i64 %1, %gep_offset
	if (Instruction *base_inst = dyn_cast<Instruction> (unwrap (base))) {
		if (base_inst->getOpcode () == Instruction::Add) {
			if (Instruction *base_ptr_ins = dyn_cast<Instruction> (base_inst->getOperand (0))) {
				if (base_ptr_ins->getOpcode () == Instruction::PtrToInt) {
					*gep_base = wrap (base_ptr_ins->getOperand (0));
					*gep_offset = wrap (base_inst->getOperand (1));
					return TRUE;
				}
			}
		}
	}
	return FALSE;
}

gboolean
mono_llvm_is_nonnull (LLVMValueRef wrapped)
{
	// Argument to function
	Value *val = unwrap (wrapped);

	while (val) {
		if (Argument *arg = dyn_cast<Argument> (val)) {
			return arg->hasNonNullAttr ();
		} else if (CallBase *calli = dyn_cast<CallBase> (val)) {
			return calli->hasRetAttr (Attribute::NonNull);
		} else if (LoadInst *loadi = dyn_cast<LoadInst> (val)) {
			return loadi->getMetadata("nonnull") != nullptr; // nonnull <index>
		} else if (Instruction *inst = dyn_cast<Instruction> (val)) {
			// If not a load or a function argument, the only case for us to
			// consider is that it's a bitcast. If so, recurse on what was casted.
			if (inst->getOpcode () == LLVMBitCast) {
				val = inst->getOperand (0);
				continue;
			}

			return FALSE;
		} else {
			return FALSE;
		}
	}
	return FALSE;
}

GSList *
mono_llvm_calls_using (LLVMValueRef wrapped_local)
{
	GSList *usages = NULL;
	Value *local = unwrap (wrapped_local);

	for (User *user : local->users ()) {
		if (isa<CallBase> (user))
			usages = g_slist_prepend (usages, wrap (user));
	}

	return usages;
}

LLVMValueRef *
mono_llvm_call_args (LLVMValueRef wrapped_calli)
{
	Value *calli = unwrap(wrapped_calli);
	CallBase *call = dyn_cast <CallBase> (calli);
	g_assert (call);

	unsigned numOperands = call->arg_size ();

	LLVMValueRef *ret = g_malloc (sizeof (LLVMValueRef) * numOperands);

	for (unsigned i = 0; i < numOperands; i++)
		ret [i] = wrap (call->getArgOperand (i));

	return ret;
}

void
mono_llvm_set_call_notailcall (LLVMValueRef func)
{
	unwrap<CallInst>(func)->setTailCallKind (CallInst::TailCallKind::TCK_NoTail);
}

void
mono_llvm_set_call_noalias_ret (LLVMValueRef wrapped_calli)
{
	add_ret_attr (wrapped_calli, Attribute::NoAlias);
}

void
mono_llvm_set_alignment_ret (LLVMValueRef call, int alignment)
{
	Instruction *ins = unwrap<Instruction> (call);
	auto &ctx = ins->getContext ();

#if LLVM_API_VERSION >= 1400
	dyn_cast<CallBase>(ins)->addRetAttr (Attribute::getWithAlignment(ctx, to_align (alignment)));
#else
	dyn_cast<CallBase>(ins)->addAttribute (AttributeList::ReturnIndex, Attribute::getWithAlignment(ctx, to_align (alignment)));
#endif
}

static Attribute::AttrKind
convert_attr (AttrKind kind)
{
	switch (kind) {
	case LLVM_ATTR_NO_UNWIND:
		return Attribute::NoUnwind;
	case LLVM_ATTR_NO_INLINE:
		return Attribute::NoInline;
	case LLVM_ATTR_OPTIMIZE_FOR_SIZE:
		return Attribute::OptimizeForSize;
	case LLVM_ATTR_OPTIMIZE_NONE:
		return Attribute::OptimizeNone;
	case LLVM_ATTR_IN_REG:
		return Attribute::InReg;
	case LLVM_ATTR_STRUCT_RET:
		return Attribute::StructRet;
	case LLVM_ATTR_NO_ALIAS:
		return Attribute::NoAlias;
	case LLVM_ATTR_BY_VAL:
		return Attribute::ByVal;
	case LLVM_ATTR_UW_TABLE:
		return Attribute::UWTable;
	default:
		assert (0);
		return Attribute::NoUnwind;
	}
}

void
mono_llvm_add_func_attr (LLVMValueRef func, AttrKind kind)
{
	unwrap<Function> (func)->addFnAttr (convert_attr (kind));
}

void
mono_llvm_add_func_attr_nv (LLVMValueRef func, const char *attr_name, const char *attr_value)
{
	unwrap<Function> (func)->addFnAttr (attr_name, attr_value);
}

void
mono_llvm_add_param_attr (LLVMValueRef param, AttrKind kind)
{
	Function *func = unwrap<Argument> (param)->getParent ();
	int n = unwrap<Argument> (param)->getArgNo ();
	func->addParamAttr (n, convert_attr (kind));
}

void
mono_llvm_add_param_byval_attr (LLVMValueRef param, LLVMTypeRef type)
{
	Function *func = unwrap<Argument> (param)->getParent ();
	int n = unwrap<Argument> (param)->getArgNo ();
	func->addParamAttr (n, Attribute::getWithByValType (*unwrap (LLVMGetGlobalContext ()), unwrap (type)));
}

void
mono_llvm_add_instr_attr (LLVMValueRef val, int index, AttrKind kind)
{
#if LLVM_API_VERSION >= 1400
	unwrap<CallBase> (val)->addParamAttr (index - 1, convert_attr (kind));
#else
	unwrap<CallBase> (val)->addAttribute (index, convert_attr (kind));
#endif
}

void
mono_llvm_add_instr_byval_attr (LLVMValueRef val, int index, LLVMTypeRef type)
{
#if LLVM_API_VERSION >= 1400
	unwrap<CallBase> (val)->addParamAttr (index - 1, Attribute::getWithByValType (*unwrap (LLVMGetGlobalContext ()), unwrap (type)));
#else
	unwrap<CallBase> (val)->addAttribute (index, Attribute::getWithByValType (*unwrap (LLVMGetGlobalContext ()), unwrap (type)));
#endif
}

void*
mono_llvm_create_di_builder (LLVMModuleRef module)
{
	return new DIBuilder (*unwrap(module));
}

void*
mono_llvm_di_create_compile_unit (void *di_builder, const char *cu_name, const char *dir, const char *producer)
{
	DIBuilder *builder = (DIBuilder*)di_builder;

	DIFile *di_file;

	di_file = builder->createFile (cu_name, dir);
	return builder->createCompileUnit (dwarf::DW_LANG_C99, di_file, producer, true, "", 0);
}

void*
mono_llvm_di_create_function (void *di_builder, void *cu, LLVMValueRef func, const char *name, const char *mangled_name, const char *dir, const char *file, int line)
{
	DIBuilder *builder = (DIBuilder*)di_builder;
	DIFile *di_file;
	DISubroutineType *type;
	DISubprogram *di_func;

	// FIXME: Share DIFile
	di_file = builder->createFile (file, dir);
	type = builder->createSubroutineType (builder->getOrCreateTypeArray (ArrayRef<Metadata*> ()));
	di_func = builder->createFunction (
		di_file, name, mangled_name, di_file, line, type, 0,
		DINode::FlagZero, DISubprogram::SPFlagDefinition | DISubprogram::SPFlagLocalToUnit);
	unwrap<Function>(func)->setMetadata ("dbg", di_func);

	return di_func;
}

void*
mono_llvm_di_create_file (void *di_builder, const char *dir, const char *file)
{
	DIBuilder *builder = (DIBuilder*)di_builder;

	return builder->createFile (file, dir);
}

void*
mono_llvm_di_create_location (void *di_builder, void *scope, int row, int column)
{
	return DILocation::get (*unwrap(LLVMGetGlobalContext ()), row, column, (Metadata*)scope);
}

void
mono_llvm_set_fast_math (LLVMBuilderRef builder)
{
	FastMathFlags flags;
	flags.setFast ();
	unwrap(builder)->setFastMathFlags (flags);
}

void
mono_llvm_di_set_location (LLVMBuilderRef builder, void *loc_md)
{
	unwrap(builder)->SetCurrentDebugLocation ((DILocation*)loc_md);
}

void
mono_llvm_di_builder_finalize (void *di_builder)
{
	DIBuilder *builder = (DIBuilder*)di_builder;

	builder->finalize ();
}

LLVMValueRef
mono_llvm_get_or_insert_gc_safepoint_poll (LLVMModuleRef module)
{
	llvm::FunctionCallee callee = unwrap(module)->getOrInsertFunction("gc.safepoint_poll", FunctionType::get(unwrap(LLVMVoidType()), false));
	return wrap (dyn_cast<llvm::Function> (callee.getCallee ()));
}

gboolean
mono_llvm_remove_gc_safepoint_poll (LLVMModuleRef module)
{
	llvm::Function *func = unwrap (module)->getFunction ("gc.safepoint_poll");
	if (func == nullptr)
		return FALSE;
	func->eraseFromParent ();
	return TRUE;
}

int
mono_llvm_check_cpu_features (const CpuFeatureAliasFlag *features, int length)
{
	int flags = 0;
	llvm::StringMap<bool> HostFeatures;
	if (llvm::sys::getHostCPUFeatures (HostFeatures)) {
		for (int i=0; i<length; i++) {
			CpuFeatureAliasFlag feature = features [i];
			if (HostFeatures [feature.alias])
				flags |= feature.flag;
		}
		/*
		for (auto &F : HostFeatures)
			if (F.second)
				outs () << "X: " << F.first () << "\n";
		*/
	}
	return flags;
}

static const Intrinsic::ID not_intrinsic =
	Intrinsic::IndependentIntrinsics::not_intrinsic;

/* Map our intrinsic ID to the LLVM intrinsic id */
static Intrinsic::ID
get_intrins_id (IntrinsicId id)
{
	Intrinsic::ID intrins_id = not_intrinsic;
	switch (id) {
#define Generic IndependentIntrinsics
#define X86 X86Intrinsics
#define Arm64 AARCH64Intrinsics
#define Wasm WASMIntrinsics
#define INTRINS(id, llvm_id, arch) case INTRINS_ ## id: intrins_id = Intrinsic::arch::llvm_id; break;

#define INTRINS_OVR(id, llvm_id, arch, ty) INTRINS(id, llvm_id, arch)
#define INTRINS_OVR_2_ARG(id, llvm_id, arch, ty1, ty2) INTRINS(id, llvm_id, arch)
#define INTRINS_OVR_3_ARG(id, llvm_id, arch, ty1, ty2, ty3) INTRINS(id, llvm_id, arch)
#define INTRINS_OVR_TAG(id, llvm_id, arch, ...) INTRINS(id, llvm_id, arch)
#define INTRINS_OVR_TAG_KIND(id, llvm_id, arch, ...) INTRINS(id, llvm_id, arch)
#include "llvm-intrinsics.h"
	default:
		break;
	}
	return intrins_id;
}

static bool
is_overloaded_intrins (IntrinsicId id)
{
	switch (id) {
#define INTRINS(id, llvm_id, arch)
#define INTRINS_OVR(id, llvm_id, arch, ty) case INTRINS_ ## id: return true;
#define INTRINS_OVR_2_ARG(id, llvm_id, arch, ty1, ty2) case INTRINS_ ## id: return true;
#define INTRINS_OVR_3_ARG(id, llvm_id, arch, ty1, ty2, ty3) case INTRINS_ ## id: return true;
#define INTRINS_OVR_TAG(id, llvm_id, arch, ...) case INTRINS_ ## id: return true;
#define INTRINS_OVR_TAG_KIND(id, llvm_id, arch, ...) case INTRINS_ ## id: return true;
#include "llvm-intrinsics.h"
	default:
		break;
	}
	return false;
}

/*
 * mono_llvm_register_intrinsic:
 *
 *   Register an LLVM intrinsic identified by ID.
 */
LLVMValueRef
mono_llvm_register_intrinsic (LLVMModuleRef module, IntrinsicId id)
{
	if (is_overloaded_intrins (id))
		return NULL;

	auto intrins_id = get_intrins_id (id);
	if (intrins_id != not_intrinsic) {
		Function *f = Intrinsic::getDeclaration (unwrap (module), intrins_id);
		if (!f) {
			outs () << id << "\n";
			g_assert_not_reached ();
		}
		return wrap (f);
	} else {
		return NULL;
	}
}

/*
 * mono_llvm_register_intrinsic:
 *
 *   Register an overloaded LLVM intrinsic identified by ID using the supplied types.
 */
LLVMValueRef
mono_llvm_register_overloaded_intrinsic (LLVMModuleRef module, IntrinsicId id, LLVMTypeRef *types, int ntypes)
{
	auto intrins_id = get_intrins_id (id);

	const int max_types = 5;
	g_assert (ntypes <= max_types);
	Type *arr [max_types];
	for (int i = 0; i < ntypes; ++i)
		arr [i] = unwrap (types [i]);
	auto f = Intrinsic::getDeclaration (unwrap (module), intrins_id, { arr, (size_t)ntypes });
	return wrap (f);
}

unsigned int
mono_llvm_get_prim_size_bits (LLVMTypeRef type)
{
	return unwrap (type)->getPrimitiveSizeInBits ();
}

/*
 * Inserts a call to a fragment of inline assembly.
 *
 * Return values correspond to output constraints. Parameter values correspond
 * to input constraints. Example of use:
 * mono_llvm_inline_asm (builder, void_func_t, "int $$0x3", "", LLVM_ASM_SIDE_EFFECT, NULL, 0, "");
 */
LLVMValueRef
mono_llvm_inline_asm (LLVMBuilderRef builder, LLVMTypeRef type,
	const char *asmstr, const char *constraints,
	MonoLLVMAsmFlags flags, LLVMValueRef *args, unsigned num_args,
	const char *name)
{
	const auto asmstr_len = strlen (asmstr);
	const auto constraints_len = strlen (constraints);
#if LLVM_API_VERSION >= 1300
	const auto asmval = LLVMGetInlineAsm (type,
		const_cast<char *>(asmstr), asmstr_len,
		const_cast<char *>(constraints), constraints_len,
		(flags & LLVM_ASM_SIDE_EFFECT) != 0, (flags & LLVM_ASM_ALIGN_STACK) != 0,
										  LLVMInlineAsmDialectATT, TRUE);
#else
	const auto asmval = LLVMGetInlineAsm (type,
		const_cast<char *>(asmstr), asmstr_len,
		const_cast<char *>(constraints), constraints_len,
		(flags & LLVM_ASM_SIDE_EFFECT) != 0, (flags & LLVM_ASM_ALIGN_STACK) != 0,
		LLVMInlineAsmDialectATT);
#endif
	return LLVMBuildCall2 (builder, type, asmval, args, num_args, name);
}
