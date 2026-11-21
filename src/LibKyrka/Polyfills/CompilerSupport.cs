// Types the C# compiler needs for records, init-only setters and
// [CallerArgumentExpression]. Modern .NET ships them; the compiler only
// looks them up by name, so internal copies work.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }
}

namespace System.Threading
{
    /// <summary>
    /// Stand-in for System.Threading.Lock (.NET 9+), built on <see cref="Monitor"/>.
    /// The compiler recognizes the type by name and turns a <c>lock</c>
    /// statement into <c>using (x.EnterScope())</c>.
    /// </summary>
    internal sealed class Lock
    {
        private readonly object _monitor = new();

        public Scope EnterScope()
        {
            Monitor.Enter(_monitor);
            return new Scope(_monitor);
        }

        public readonly ref struct Scope(object monitor)
        {
            public void Dispose() => Monitor.Exit(monitor);
        }
    }
}
