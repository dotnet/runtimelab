using System.Collections.Generic;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A collection of connection properties.
    /// </summary>
    public sealed class ConnectionProperties : IConnectionProperties
    {
        private readonly Dictionary<Type, object?> _values = new();
        private bool _frozen;

        /// <summary>
        /// Adds a new property to the conenction.
        /// </summary>
        /// <typeparam name="T">The type of property to add.</typeparam>
        /// <param name="propertyKey">The key of the property.</param>
        /// <param name="value">The property's value.</param>
        public void Add<T>(ConnectionPropertyKey<T> propertyKey, T value)
        {
            if (_frozen) throw new InvalidOperationException($"The {nameof(ConnectionProperties)} can not be altered after a property has been retrieved.");
            _values.Add(typeof(T), value);
        }

        /// <inheritdoc/>
        public bool TryGetProperty(Type type, out object? value)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            _frozen = true;
            return _values.TryGetValue(type, out value);
        }
    }
}
