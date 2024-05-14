using System.Reflection;
using System.Diagnostics.CodeAnalysis;


namespace BindingsGeneration;

using ITypeHandlerFactory = IFactory<BaseDecl, ITypeHandler>;
using IMethodHandlerFactory = IFactory<MethodDecl, IMethodHandler>;
using IArgumentHandlerFactory = IFactory<ArgumentDecl, IArgumentHandler>;
public class Conductor {
    List<ITypeHandlerFactory> typeHandlerFactories = new List<ITypeHandlerFactory>();
    List<IMethodHandlerFactory> methodHandlerFactories = new List<IMethodHandlerFactory>();
    List<IArgumentHandlerFactory> argumentHandlerFactories = new List<IArgumentHandlerFactory>();
    
    public Conductor()
    {
        LoadTypeHandlers(typeHandlerFactories);
        LoadMethodHandlers(methodHandlerFactories);
        LoadArgumentHandlers(argumentHandlerFactories);
    }

    public bool TryGetTypeHandler(BaseDecl baseDecl, [NotNullWhen(returnValue: true)] out ITypeHandler? handler)
    {
        return TryGetFooHandler(baseDecl, out handler, typeHandlerFactories);
    }

    public bool TryGetMethodHandler(MethodDecl methodDecl, [NotNullWhen(returnValue: true)] out IMethodHandler? handler)
    {
        return TryGetFooHandler(methodDecl, out handler, methodHandlerFactories);
    }

    static bool TryGetFooHandler<T, U> (T decl, [NotNullWhen(returnValue: true)] out U? handler, List<IFactory<T, U>> factories) where U:class
    {
        var factory = factories.FirstOrDefault (f => f.Handles (decl));
        handler = factory is not null ? factory.Construct () : null;
        return handler is not null;
    }


    static void LoadTypeHandlers (List<ITypeHandlerFactory> factories)
    {
        factories.AddRange (TypesImplementing (typeof (ITypeHandlerFactory)).Select (ToTypeHandlerFactory));
    }

    static ITypeHandlerFactory ToTypeHandlerFactory (Type ty)
    {
        return CallDefaultCtor<ITypeHandlerFactory> (ty);
    }

    static void LoadMethodHandlers(List<IMethodHandlerFactory> factories)
    {
        factories.AddRange(TypesImplementing(typeof(IMethodHandlerFactory)).Select(ToMethodHandlerFactory));
    }

    static IMethodHandlerFactory ToMethodHandlerFactory (Type ty)
    {
        return CallDefaultCtor<IMethodHandlerFactory>(ty);
    }

    static void LoadArgumentHandlers(List<IArgumentHandlerFactory> factories)
    {
        factories.AddRange(TypesImplementing(typeof(IArgumentHandlerFactory)).Select(ToArgumentHandlerFactory));
    }

    static IArgumentHandlerFactory ToArgumentHandlerFactory(Type ty)
    {
        return CallDefaultCtor<IArgumentHandlerFactory>(ty);
    }

    static T CallDefaultCtor<T> (Type ty) where T : class
    {
        var ctor = ty.GetConstructor (Type.EmptyTypes);
        if (ctor is null)
            throw new Exception ($"{ty.Name} does not contain default constructor");
        return (ctor.Invoke (null, null) as T)!;
    }
    
    static IEnumerable<Type> TypesImplementing(Type targetType)
    {
        var assembly = Assembly.GetExecutingAssembly ();
        return assembly.GetTypes ().Where (t => targetType.IsAssignableFrom (t));
    }
}
