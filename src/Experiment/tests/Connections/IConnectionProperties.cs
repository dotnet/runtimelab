namespace System.Net.Http.LowLevel.Tests.Connections
{
    /// <summary>
    /// A read-only collection of connection properties.
    /// </summary>
    public interface IConnectionProperties
    {
        /// <summary>
        /// Gets a property.
        /// </summary>
        /// <param name="type">The type of the property to retrieve.</param>
        /// <param name="value">The value of the property retrieved.</param>
        /// <returns>
        /// If a property for the given <paramref name="type"/> was found, true.
        /// Otherwise, false.
        /// </returns>
        bool TryGetProperty(Type type, out object? value);
    }
}
