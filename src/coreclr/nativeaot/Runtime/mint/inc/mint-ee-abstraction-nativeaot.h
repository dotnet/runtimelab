#ifndef _MINT_EE_ABSTRACTION_NATIVEAOT_H
#define _MINT_EE_ABSTRACTION_NATIVEAOT_H

typedef struct _MintEEFrameDataFragmentNativeAot MintEEFrameDataFragmentNativeAot;
typedef struct _MintEEFrameDataInfoNativeAot MintEEFrameDataInfoNativeAot;

typedef struct _MintEEFrameDataAllocatorNativeAot {
    MintEEFrameDataFragmentNativeAot* first;
    MintEEFrameDataFragmentNativeAot* current;
    MintEEFrameDataInfoNativeAot* infos;
    int32_t infos_len;
    int32_t infos_capacity;
    /* For GC sync */
    int32_t inited;
} MintEEFrameDataAllocatorNativeAot;

typedef struct _MintEEThreadContextInstanceAbstractionNativeAot {
    uint8_t *stack_pointer;
    uint8_t* stack_start;
    uint8_t* stack_end;
    uint8_t* stack_real_end;

    MintEEFrameDataAllocatorNativeAot data_stack;

    void (*set_stack_pointer)(ThreadContext* context, uint8_t *stack_pointer);
    int32_t (*check_sufficient_stack)(ThreadContext* context, uintptr_t size);
} MintEEThreadContextInstanceAbstractionNativeAot;

typedef struct _MintEEAbstractionNativeAot {
    void (*tls_initialize)(void);
    ThreadContext* (*get_context)(void);

    MintEEThreadContextInstanceAbstractionNativeAot* (*get_ThreadContext_inst)(ThreadContext *context);
} MintEEAbstractionNativeAot;

MintEEAbstractionNativeAot *mint_ee_itf(void);

#endif/*_MINT_EE_ABSTRACTION_NATIVEAOT_H*/
