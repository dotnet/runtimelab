namespace System.Reflection.Emit.Experimental
{
    internal class EntityWrappers
    {
        internal class AssemmblyReferenceWrapper
        {
            internal readonly Assembly assembly;
            public AssemmblyReferenceWrapper(Assembly assembly)
            {
                this.assembly = assembly;
            }
            public override bool Equals(object? obj)
            {
                var item = obj as AssemmblyReferenceWrapper;

                if (item == null)
                {
                    return false;
                }

                return assembly.GetName().Equals(item.assembly.GetName());
            }
            public override int GetHashCode()
            {
                return assembly.GetName().GetHashCode();
            }
        }

        internal class TypeReferenceWrapper
        {
            internal readonly Type type;
            internal int parentToken = 0;
            public TypeReferenceWrapper(Type type)
            {
                this.type = type;
            }
            public override bool Equals(object? obj)
            {
                var item = obj as TypeReferenceWrapper;

                if (item == null)
                {
                    return false;
                }
                bool equality = type.Name.Equals(item.type.Name) && parentToken== item.parentToken;
                if(type.Namespace!=null)
                {
                    equality &= type.Namespace.Equals(item.type.Namespace);
                }
                return equality;
            }
            public override int GetHashCode()
            {
                return type.GetHashCode();
            }
        }

        internal class MethodReferenceWrapper
        {
            internal readonly MethodBase method;
            internal int parentToken = 0;
            public MethodReferenceWrapper(MethodBase method)
            {
                this.method = method;
            }
            public override bool Equals(object? obj)
            {
                var item = obj as MethodReferenceWrapper;

                if (item == null)
                {
                    return false;
                }
                bool equality = method.Name.Equals(item.method.Name) && parentToken == item.parentToken;
                return equality;
            }
            public override int GetHashCode()
            {
                return method.GetHashCode();
            }
        }

        internal class CustomAttribute
        {
            internal ConstructorInfo con;
            internal byte[] binaryAttribute;
            internal int conToken = 0;


            public CustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
            {
                this.con = con;
                this.binaryAttribute = binaryAttribute;
            }
        }



    }
}
