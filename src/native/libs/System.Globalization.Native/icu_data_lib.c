#define STR_IMPL(x) #x
#define STR(x) STR_IMPL(x)

// Ideally, we would use actual assembly (.S), but Cmake doesn't recognize emcc as a valid assembler.
__asm(
    "  .section .data,\"\",@\n"
    "  .global static_icu_data\n"
    "  .align 16\n" // https://unicode-org.github.io/icu/userguide/icu_data/#alignment
    "static_icu_data:\n"
    "  .incbin \"" STR(ICU_DATA_FILE) "\"\n"
    "static_icu_data_end:\n"
    "  .size static_icu_data, static_icu_data_end - static_icu_data\n"
    "  .size static_icu_data_end, 1\n"
);
