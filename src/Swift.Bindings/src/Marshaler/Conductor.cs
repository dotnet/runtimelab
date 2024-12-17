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
    public class Conductor
    {
        private readonly List<IModuleHandlerFactory> _moduleHandlerFactories = [
            new ModuleHandlerFactory()
        ];
        private readonly List<ITypeHandlerFactory> _typeHandlerFactories = [
            new NonFrozenStructHandlerFactory(),
            new FrozenStructHandlerFactory(),
            new ClassHandlerFactory(),
        ];
        private readonly List<IFieldHandlerFactory> _fieldHandlerFactories = [];
        private readonly List<IMethodHandlerFactory> _methodHandlerFactories = [
            new MethodHandlerFactory(),
            new ConstructorHandlerFactory(),
        ];
        private readonly List<IArgumentHandlerFactory> _argumentHandlerFactories = [];

        /// <summary>
        /// Initializes a new instance of the Conductor class and loads all handler factories.
        /// </summary>
        public Conductor()
        {
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
    }
}
