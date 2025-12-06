using System.Collections.Concurrent;
using Raylib_cs;

namespace WarpSimulation;

internal static class Program
{
    // lock object for console
    private static System.Threading.Lock s_consoleLock = new();

    // input buffer
    private static string s_inputBuffer = string.Empty;

    // a concurrent queue to hold commands from the input thread
    private static ConcurrentQueue<string> s_commandQueue = new();

    // STAThread is required if you deploy using NativeAOT on Windows - See https://github.com/raylib-cs/raylib-cs/issues/301
    [System.STAThread]
    public static void Main(string[] args)
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(1280, 720, "WARP Network Simulation");
        Raylib.SetTargetFPS(60);

        Simulation simulation = Simulation.Instance;

        if (args.Length > 0)
        {
            string jsonString = System.IO.File.ReadAllText(args[0]);
            simulation.LoadFromJsonFile(jsonString);
        }

        var thread = new Thread(InputThread);
        thread.IsBackground = true;
        thread.Start();

        while (!Raylib.WindowShouldClose())
        {
            while (s_commandQueue.TryDequeue(out string? input))
            {
                input = input.Trim();
                var inputs = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                string command = inputs[0];
                string[] argList = inputs.Skip(1).ToArray();

                simulation.ProcessCommand(command, argList);
            }

            lock (s_consoleLock)
            {
                RedrawInputLine();
            }

            float delta = Raylib.GetFrameTime();

            simulation.Update(delta);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);
            simulation.Draw();
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }

    static void InputThread()
    {
        lock (s_consoleLock)
        {
            RedrawInputLine();
        }

        while (!Raylib.WindowShouldClose())
        {
            if (Console.IsInputRedirected)
            {
                string? line = Console.ReadLine();
                if (line != null)
                {
                    s_commandQueue.Enqueue(line);
                    continue;
                }
                else
                {
                    break;
                }
            }

            var key = Console.ReadKey(intercept: true);
            bool ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;

            lock (s_consoleLock)
            {
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);

                    string command = s_inputBuffer;
                    s_inputBuffer = "";

                    s_commandQueue.Enqueue(command);
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (s_inputBuffer.Length > 0)
                    {
                        s_inputBuffer = s_inputBuffer[..^1];
                    }
                }
                else if (ctrl && key.Key == ConsoleKey.D)
                {
                    Environment.Exit(0);
                }
                else
                {
                    s_inputBuffer += key.KeyChar;
                }
            }
        }
    }

    private static void RedrawInputLine()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        int bottom = Console.WindowTop + Console.WindowHeight - 1;

        // move to bottom line
        Console.SetCursorPosition(0, bottom);

        // clear the line
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, bottom);

        // write the prompt and input buffer
        Console.Write("> " + s_inputBuffer);
    }

    internal static void WriteOutput(string text)
    {
        lock (s_consoleLock)
        {
            if (Console.IsOutputRedirected)
            {
                Console.WriteLine(text);
                return;
            }

            int bottom = Console.WindowTop + Console.WindowHeight - 1;

            Console.SetCursorPosition(0, bottom);
            Console.WriteLine();

            Console.WriteLine(text);
        }
    }
}
