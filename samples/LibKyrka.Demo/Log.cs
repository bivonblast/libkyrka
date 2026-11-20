namespace LibKyrka.Demo;

/// <summary>Minimal colored console output.</summary>
internal static class Log
{
    private static readonly Lock Gate = new();

    public static void Info(string source, string message) => Write(ConsoleColor.Green, source, message);

    public static void Event(string source, string message) => Write(ConsoleColor.DarkGray, source, message);

    public static void Warn(string source, string message) => Write(ConsoleColor.Yellow, source, message);

    public static void Error(string message) => Write(ConsoleColor.Red, "demo", message);

    private static void Write(ConsoleColor color, string source, string message)
    {
        lock (Gate)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{source,-5}] {message}");
            Console.ResetColor();
        }
    }
}
