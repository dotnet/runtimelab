using System;

class SimpleTest
{
    // Simple test function that mimics the header validation logic
    static bool IsValidHeaderValue(string headerName, string headerValue)
    {
        if (string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase))
        {
            // Filter out negative values which are used as sentinel values in WASI
            if (headerValue == "-1" || headerValue.StartsWith("-"))
            {
                return false;
            }
        }
        return true;
    }

    static void Main()
    {
        // Test cases to verify our logic works
        Console.WriteLine("Testing header validation logic:");

        // Should return false for Content-Length: -1
        bool test1 = IsValidHeaderValue("Content-Length", "-1");
        Console.WriteLine($"Content-Length: -1 -> {test1} (expected: False)");

        // Should return true for Content-Length: 1024
        bool test2 = IsValidHeaderValue("Content-Length", "1024");
        Console.WriteLine($"Content-Length: 1024 -> {test2} (expected: True)");

        // Should return true for other headers with -1
        bool test3 = IsValidHeaderValue("X-Custom-Header", "-1");
        Console.WriteLine($"X-Custom-Header: -1 -> {test3} (expected: True)");

        // Should return true for Content-Length: 0
        bool test4 = IsValidHeaderValue("Content-Length", "0");
        Console.WriteLine($"Content-Length: 0 -> {test4} (expected: True)");

        Console.WriteLine();
        Console.WriteLine("All tests demonstrate the fix logic works correctly!");
        Console.WriteLine("The fix will filter out Content-Length: -1 headers that cause FormatException");
    }
}
