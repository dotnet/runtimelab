#ifndef _MINT_ABSTRACTION_H
#define _MINT_ABSTRACTION_H

#ifndef NATIVEAOT_MINT
#define MINT_ITF(sym) sym
#define MINT_TITF(type,sym) sym
#define MINT_TI_ITF(type,self,sym) self->sym
#define MINT_ITF_DEFAULT_BYVAL_TYPE(type) m_class_get_byval_arg (mono_defaults. type ## _class)
#define MINT_ITF_DEFAULT_CLASS(klass) mono_defaults. klass ## _class
#else
#define MINT_ITF(sym) mint_itf()->sym
#define MINT_TITF(type,sym) mint_itf()->get_ ##type () -> sym
#define MINT_TI_ITF(type,self,sym) mint_itf()->get_ ## type ## _inst (self) -> sym
#define MINT_ITF_DEFAULT_BYVAL_TYPE(type) mint_itf()->get_default_byval_type_ ## type ()
#define MINT_ITF_DEFAULT_CLASS(klass) mint_itf()->get_default_class_ ## klass ## _class ()
#endif


#endif/*_MINT_ABSTRACTION_H*/
