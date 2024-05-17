// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace BindingsGeneration
{
    using IModuleHandlerFactory = IFactory<BaseDecl, IModuleHandler>;
    using ITypeHandlerFactory = IFactory<BaseDecl, ITypeHandler>;
    using IFieldHandlerFactory = IFactory<BaseDecl, IFieldHandler>;
    using IMethodHandlerFactory = IFactory<BaseDecl, IMethodHandler>;
    using IArgumentHandlerFactory = IFactory<BaseDecl, IArgumentHandler>;

    /// <summary>
    /// The Conductor class is responsible for managing handler factories and retrieving specific handlers for declarations.
    /// It initializes the handler factories and provides methods to fetch handlers for given declarations.
    /// </summary>
    public class Conductor {
        private readonly List<IModuleHandlerFactory> _moduleHandlerFactories = [];
        private readonly List<ITypeHandlerFactory> _typeHandlerFactories = [];
        private readonly List<IFieldHandlerFactory> _fieldHandlerFactories = [];
        private readonly List<IMethodHandlerFactory> _methodHandlerFactories = [];
        private readonly List<IArgumentHandlerFactory> _argumentHandlerFactories = [];
        
        /// <summary>
        /// Initializes a new instance of the Conductor class and loads all handler factories.
        /// </summary>
        public Conductor()
        {
            LoadModuleHandlers(_moduleHandlerFactories);
            LoadTypeHandlers(_typeHandlerFactories);
            LoadFieldHandlers(_fieldHandlerFactories);
            LoadMethodHandlers(_methodHandlerFactories);
            LoadArgumentHandlers(_argumentHandlerFactories);
        }


        /// <summary>
        /// Tries to get a module handler for a given moduleDecl.
        /// </summary>
        /// <param name="moduleDecl">The module declaration to get the handler for.</param>
        /// <param name="handler">The handler found for the given declaration.</param>
        /// <returns>True if a handler is found, otherwise false.</returns>
        public bool TryGetModuleHandler(ModuleDecl moduleDecl, [NotNullWhen(returnValue: true)] out IModuleHandler? handler)
        {
            return TryGetFooHandler(moduleDecl, out handler, _moduleHandlerFactories);
        }

        /// <summary>
        /// Tries to get a type handler for a given typeDecl.
        /// </summary>
        /// <param name="typeDecl">The type declaration to get the handler for.</param>
        /// <param name="handler">The handler found for the given declaration.</param>
        /// <returns>True if a handler is found, otherwise false.</returns>
        public bool TryGetTypeHandler(TypeDecl typeDecl, [NotNullWhen(returnValue: true)] out ITypeHandler? handler)
        {
            return TryGetFooHandler(typeDecl, out handler, _typeHandlerFactories);
        }

        /// <summary>
        /// Tries to get a method handler for a given MethodDecl.
        /// </summary>
        /// <param name="methodDecl">The method declaration to get the handler for.</param>
        /// <param name="handler">The handler found for the given declaration.</param>
        /// <returns>True if a handler is found, otherwise false.</returns>
        public bool TryGetMethodHandler(MethodDecl methodDecl, [NotNullWhen(returnValue: true)] out IMethodHandler? handler)
        {
            return TryGetFooHandler(methodDecl, out handler, _methodHandlerFactories);
        }

        /// <summary>
        /// Tries to get an argument handler for a given ArgumentDecl.
        /// </summary>
        /// <param name="argumentDecl">The argument declaration to get the handler for.</param>
        /// <param name="handler">The handler found for the given declaration.</param>
        /// <returns>True if a handler is found, otherwise false.</returns>
        public bool TryGetArgumentHandler(ArgumentDecl argumentDecl, [NotNullWhen(returnValue: true)] out IArgumentHandler? handler)
        {
            return TryGetFooHandler(argumentDecl, out handler, _argumentHandlerFactories);
        }

        /// <summary>
        /// Tries to get a handler for a given declaration using the specified factories.
        /// </summary>
        /// <typeparam name="T">The type of the declaration.</typeparam>
        /// <typeparam name="U">The type of the handler.</typeparam>
        /// <param name="decl">The declaration to get the handler for.</param>
        /// <param name="handler">The handler found for the given declaration.</param>
        /// <param name="factories">The list of factories to search for a handler.</param>
        /// <returns>True if a handler is found, otherwise false.</returns>
        static bool TryGetFooHandler<T, U>(T decl, [NotNullWhen(returnValue: true)] out U? handler, List<IFactory<T, U>> factories) where U : class
        {
            var factory = factories.FirstOrDefault(f => f.Handles(decl));
            handler = factory?.Construct();
            return handler is not null;
        }

        /// <summary>
        /// Loads module handler factories from the assembly.
        /// </summary>
        /// <param name="factories">The list to add the loaded factories to.</param>
        private static void LoadModuleHandlers(List<IModuleHandlerFactory> factories)
        {
            factories.AddRange(TypesImplementing(typeof(IModuleHandlerFactory)).Select(ToModuleHandlerFactory));
        }

        /// <summary>
        /// Converts a type to an IModuleHandlerFactory instance.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The IModuleHandlerFactory instance.</returns>
        private static IModuleHandlerFactory ToModuleHandlerFactory(Type type)
        {
            return CallDefaultCtor<IModuleHandlerFactory>(type);
        }

        /// <summary>
        /// Loads type handler factories from the assembly.
        /// </summary>
        /// <param name="factories">The list to add the loaded factories to.</param>
        private static void LoadTypeHandlers(List<ITypeHandlerFactory> factories)
        {
            factories.AddRange(TypesImplementing(typeof(ITypeHandlerFactory)).Select(ToTypeHandlerFactory));
        }

        /// <summary>
        /// Converts a type to an ITypeHandlerFactory instance.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The ITypeHandlerFactory instance.</returns>
        private static ITypeHandlerFactory ToTypeHandlerFactory(Type type)
        {
            return CallDefaultCtor<ITypeHandlerFactory>(type);
        }

        /// <summary>
        /// Loads field handler factories from the assembly.
        /// </summary>
        /// <param name="factories">The list to add the loaded factories to.</param>
        private static void LoadFieldHandlers(List<IFieldHandlerFactory> factories)
        {
            factories.AddRange(TypesImplementing(typeof(IFieldHandlerFactory)).Select(ToFieldHandlerFactory));
        }

        /// <summary>
        /// Converts a type to an IFieldHandlerFactory instance.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The IFieldHandlerFactory instance.</returns>
        private static IFieldHandlerFactory ToFieldHandlerFactory(Type type)
        {
            return CallDefaultCtor<IFieldHandlerFactory>(type);
        }

        /// <summary>
        /// Loads method handler factories from the assembly.
        /// </summary>
        /// <param name="factories">The list to add the loaded factories to.</param>
        private static void LoadMethodHandlers(List<IMethodHandlerFactory> factories)
        {
            factories.AddRange(TypesImplementing(typeof(IMethodHandlerFactory)).Select(ToMethodHandlerFactory));
        }

        /// <summary>
        /// Converts a type to an IMethodHandlerFactory instance.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The IMethodHandlerFactory instance.</returns>
        private static IMethodHandlerFactory ToMethodHandlerFactory(Type type)
        {
            return CallDefaultCtor<IMethodHandlerFactory>(type);
        }

        /// <summary>
        /// Loads argument handler factories from the assembly.
        /// </summary>
        /// <param name="factories">The list to add the loaded factories to.</param>
        private static void LoadArgumentHandlers(List<IArgumentHandlerFactory> factories)
        {
            factories.AddRange(TypesImplementing(typeof(IArgumentHandlerFactory)).Select(ToArgumentHandlerFactory));
        }

        /// <summary>
        /// Converts a type to an IArgumentHandlerFactory instance.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The IArgumentHandlerFactory instance.</returns>
        private static IArgumentHandlerFactory ToArgumentHandlerFactory(Type type)
        {
            return CallDefaultCtor<IArgumentHandlerFactory>(type);
        }

        /// <summary>
        /// Calls the default constructor of a given type.
        /// </summary>
        /// <typeparam name="T">The type to instantiate.</typeparam>
        /// <param name="type">The type to instantiate.</param>
        /// <returns>The instantiated object.</returns>
        private static T CallDefaultCtor<T>(Type type) where T : class
        {
            var ctor = type.GetConstructor(Type.EmptyTypes) ?? throw new Exception($"{type.Name} does not contain a default constructor");
            return (ctor.Invoke(null) as T)!;
        }
        
        /// <summary>
        /// Finds all types in the assembly that implement a given target type.
        /// </summary>
        /// <param name="targetType">The target type to search for.</param>
        /// <returns>An enumerable of types that implement the target type.</returns>
        private static IEnumerable<Type> TypesImplementing(Type targetType)
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetTypes().Where(targetType.IsAssignableFrom);
        }
    }
}
