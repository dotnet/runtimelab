using System.Diagnostics.CodeAnalysis;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// Extension methods for <see cref="IConnectionProperties"/>
    /// </summary>
    public static class ConnectionPropertyExtensions
    {
        /// <summary>
        /// Gets a property, throwing an exception if none found.
        /// </summary>
        /// <typeparam name="T">The type of the property to retrieve.</typeparam>
        /// <param name="properties">The <see cref="IConnectionProperties"/> to retrieve the property from.</param>
        /// <param name="propertyKey">The key of the property.</param>
        /// <returns>The value of the retrieved property.</returns>
        public static T GetProperty<T>(this IConnectionProperties properties, ConnectionPropertyKey<T> propertyKey)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            if (properties.TryGetProperty(typeof(T), out object? objectValue) && objectValue is T typedValue)
            {
                return typedValue;
            }

            throw new Exception($"Property of type '{nameof(T)}' does not exist in this {nameof(IConnectionProperties)}, but is required.");
        }

        /// <summary>
        /// Gets a property.
        /// </summary>
        /// <typeparam name="T">The type of the property to retrieve.</typeparam>
        /// <param name="properties">The <see cref="IConnectionProperties"/> to retrieve the property from.</param>
        /// <param name="propertyKey">The key of the property.</param>
        /// <param name="value">The value of the property retrieved.</param>
        /// <returns>
        /// If a property for the given <paramref name="propertyKey"/> was found, true.
        /// Otherwise, false.
        /// </returns>
        public static bool TryGetProperty<T>(this IConnectionProperties properties, ConnectionPropertyKey<T> propertyKey, [MaybeNullWhen(false)] out T value)
        {
            if (properties == null) throw new ArgumentNullException(nameof(properties));

            if (properties.TryGetProperty(typeof(T), out object? objectValue) && objectValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }
    }
}
