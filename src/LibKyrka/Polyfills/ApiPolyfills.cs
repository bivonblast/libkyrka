using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LibKyrka
{
    // Declared in the LibKyrka namespace so no using directive is needed.
    // C# extension members add the modern static helpers to the existing
    // types, so call sites read exactly as they do on modern .NET.

    internal static class ArgumentExceptionPolyfills
    {
        extension(ArgumentNullException)
        {
            public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
            {
                if (argument is null)
                {
                    throw new ArgumentNullException(paramName);
                }
            }
        }

        extension(ArgumentException)
        {
            public static void ThrowIfNullOrEmpty(string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
            {
                ArgumentNullException.ThrowIfNull(argument, paramName);
                if (argument!.Length == 0)
                {
                    throw new ArgumentException("The value cannot be an empty string.", paramName);
                }
            }
        }

        extension(ArgumentOutOfRangeException)
        {
            public static void ThrowIfLessThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            {
                if (value < other)
                {
                    throw new ArgumentOutOfRangeException(paramName, value, $"Must be greater than or equal to {other}.");
                }
            }

            public static void ThrowIfGreaterThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            {
                if (value > other)
                {
                    throw new ArgumentOutOfRangeException(paramName, value, $"Must be less than or equal to {other}.");
                }
            }
        }

        extension(ObjectDisposedException)
        {
            public static void ThrowIf(bool condition, object instance)
            {
                if (condition)
                {
                    throw new ObjectDisposedException(instance.GetType().FullName);
                }
            }
        }
    }

    internal static class MarshalPolyfills
    {
        extension(Marshal)
        {
            public static unsafe string? PtrToStringUTF8(nint pointer)
            {
                if (pointer == 0)
                {
                    return null;
                }

                var bytes = (byte*)pointer;
                var length = 0;
                while (bytes[length] != 0)
                {
                    length++;
                }

                return Encoding.UTF8.GetString(bytes, length);
            }
        }
    }
}

namespace System.Security.Cryptography
{
    /// <summary>Stand-in for CryptographicOperations (.NET Core 2.1+).</summary>
    internal static class CryptographicOperations
    {
        /// <summary>Zeroes memory in a way the JIT will not optimize away.</summary>
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void ZeroMemory(Span<byte> buffer) => buffer.Clear();
    }
}
