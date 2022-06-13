using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;


namespace System.Reflection.Emit.Experimental
{
    class AssemblyBuilder: System.Reflection.Assembly
    {
        internal AssemblyBuilder() { }

        public void Save(string assemblyFileName)
        {
//New method
        }

        public static System.Reflection.Emit.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access) { throw new NotImplementedException("Under construction"); }// code forthecoming
        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Defining a dynamic assembly requires dynamic code.")]

        public static System.Reflection.Emit.AssemblyBuilder DefineDynamicAssembly(System.Reflection.AssemblyName name, System.Reflection.Emit.AssemblyBuilderAccess access, System.Collections.Generic.IEnumerable<System.Reflection.Emit.CustomAttributeBuilder>? assemblyAttributes) { throw new NotImplementedException("Under construction"); }
        public System.Reflection.Emit.ModuleBuilder DefineDynamicModule(string name) { throw new NotImplementedException("Under construction"); }

        public System.Reflection.Emit.ModuleBuilder? GetDynamicModule(string name) { throw new NotImplementedException("Under construction"); }
    }
}

