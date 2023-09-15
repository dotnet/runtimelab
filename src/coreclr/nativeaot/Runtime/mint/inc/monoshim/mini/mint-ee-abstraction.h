#ifndef _MINT_EE_ABSTRACTION_H
#define _MINT_EE_ABSTRACTION_H

#ifndef NATIVEAOT_MINT
#define MINT_EE_ITF(sym) sym
#define MINT_EE_TITF(type,sym) sym
#define MINT_EE_TI_ITF(type,self,sym) self->sym
#else
#define MINT_EE_ITF(sym) mint_ee_itf()->sym
#define MINT_EE_TITF(type,sym) mint_ee_itf()->get_ ##type () -> sym
#define MINT_EE_TI_ITF(type,self,sym) mint_ee_itf()->get_ ## type ## _inst (self) -> sym
#endif

#endif/*_MINT_EE_ABSTRACTION_H*/
