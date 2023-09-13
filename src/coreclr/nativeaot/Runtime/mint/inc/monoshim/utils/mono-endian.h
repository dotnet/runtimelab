#ifndef _MONOSHIM_ENDIAN_H
#define _MONOSHIM_ENDIAN_H

//# if NO_UNALIGNED_ACCESS
#if 1

static inline uint16_t
mono_read16 (const unsigned char *x)
{
    typedef union {
        char c [2];
        uint16_t i;
    } mono_rint16;

	mono_rint16 r;
#if G_BYTE_ORDER == G_LITTLE_ENDIAN
	r.c [0] = x [0];
	r.c [1] = x [1];
#else
	r.c [1] = x [0];
	r.c [0] = x [1];
#endif
	return r.i;
}

static inline uint32_t
mono_read32 (const unsigned char *x)
{
    typedef union {
        char c [4];
        guint32 i;
    } mono_rint32;

	mono_rint32 r;
#if G_BYTE_ORDER == G_LITTLE_ENDIAN
	r.c [0] = x [0];
	r.c [1] = x [1];
	r.c [2] = x [2];
	r.c [3] = x [3];
#else
	r.c [3] = x [0];
	r.c [2] = x [1];
	r.c [1] = x [2];
	r.c [0] = x [3];
#endif
	return r.i;
}

static inline uint64_t
mono_read64 (const unsigned char *x)
{
    typedef union {
        char c [8];
        uint64_t i;
    } mono_rint64;

	mono_rint64 r;
#if G_BYTE_ORDER == G_LITTLE_ENDIAN
	r.c [0] = x [0];
	r.c [1] = x [1];
	r.c [2] = x [2];
	r.c [3] = x [3];
	r.c [4] = x [4];
	r.c [5] = x [5];
	r.c [6] = x [6];
	r.c [7] = x [7];
#else
	r.c [7] = x [0];
	r.c [6] = x [1];
	r.c [5] = x [2];
	r.c [4] = x [3];
	r.c [3] = x [4];
	r.c [2] = x [5];
	r.c [1] = x [6];
	r.c [0] = x [7];
#endif
	return r.i;
}


#define read16(x) (mono_read16 ((const unsigned char *)(x)))
#define read32(x) (mono_read32 ((const unsigned char *)(x)))
#define read64(x) (mono_read64 ((const unsigned char *)(x)))

# else

#define read16(x) GUINT16_FROM_LE (*((const guint16 *) (x)))
#define read32(x) GUINT32_FROM_LE (*((const guint32 *) (x)))
#define read64(x) GUINT64_FROM_LE (*((const guint64 *) (x)))

# endif

static inline void readr4(const unsigned char *x, float *dest) {
    typedef union {
        guint32 ival;
        float fval;
    } mono_rfloat;
    mono_rfloat mf;
    mf.ival = read32 ((x));
    *(dest) = mf.fval;
}

static inline void readr8(const unsigned char *x,double *dest) {
    typedef union {
        guint64 ival;
        double fval;
        unsigned char cval [8];
    } mono_rdouble;
    mono_rdouble mf;
    mf.ival = read64 ((x));
    *(dest) = mf.fval;
}



#endif
