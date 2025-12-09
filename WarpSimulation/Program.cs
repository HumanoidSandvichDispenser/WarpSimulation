using System.Collections.Concurrent;
using System.CommandLine;
using System.CommandLine.Parsing;
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

    // signals input thread to exit
    private static volatile bool s_shouldCloseInput = false;

    // STAThread is required if you deploy using NativeAOT on Windows - See https://github.com/raylib-cs/raylib-cs/issues/301
    [System.STAThread]
    public static int Main(string[] args)
    {
#region Command Line Parsing
        Option<bool> populateDb = new("--populate-db")
        {
            Description = "Populate each node's database with the actual graph on startup.",
        };

        Option<int> simulateFrames = new("--simulate-frames")
        {
            Description = "Instead of running in real-time, simulate a fixed number of 240 fps frames as fast as possible then exit.",
            DefaultValueFactory = _ => -1,
        };

        Argument<string> inputFile = new("input-file")
        {
            Description = "Path to JSON file to load initial simulation state from.",
            Arity = ArgumentArity.ExactlyOne,
        };

        var rootCommand = new RootCommand("WARP Network Simulation")
        {
            populateDb,
            inputFile,
            simulateFrames,
        };

        var parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                Console.WriteLine($"Error: {error.Message}");
            }

            return 1;
        }
#endregion

        Simulation simulation = Simulation.Instance;

        // load simulation state from JSON file
        string filename = parseResult.GetValue(inputFile)!;
        string jsonString = System.IO.File.ReadAllText(filename);
        simulation.PopulateDatabasesOnLoad = parseResult.GetValue(populateDb);
        simulation.LoadFromJsonFile(jsonString);

        Thread? thread = null;
        if (!Console.IsInputRedirected)
        {
            // start input thread to read commands from console
            thread = new Thread(InputThread);
            thread.IsBackground = true;
            thread.Start();
        }
        else
        {
            // if input is redirected, read all lines at once and enqueue the
            // commands before starting the simulation
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                s_commandQueue.Enqueue(line);
            }
        }

        // if we are simulating a fixed number of frames, run the simulation
        // loop here and exit when done
        int nFrames = (int)parseResult.GetValue(simulateFrames);
        if (nFrames > 0)
        {
            while (s_commandQueue.TryDequeue(out string? input))
            {
                ProcessInput(input, simulation);
            }

            for (int i = 0; i < nFrames; i++)
            {
                simulation.Update(1.0f / 240.0f);
            }

            if (thread != null && thread.IsAlive)
            {
                s_shouldCloseInput = true;
                thread.Join();
            }

            return 0;
        }

        // otherwise, run the normal simulation loop with Raylib window
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint);
        Raylib.SetConfigFlags(ConfigFlags.AlwaysRunWindow);
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(640, 480, "WARP Network Simulation");
        Raylib.SetTargetFPS(60);

        while (!Raylib.WindowShouldClose())
        {
            while (s_commandQueue.TryDequeue(out string? input))
            {
                ProcessInput(input, simulation);
            }

            lock (s_consoleLock)
            {
                RedrawInputLine();
            }

            float delta = Raylib.GetFrameTime();

            // measure frame time
            double start = Raylib.GetTime();
            simulation.Update(delta);
            double end = Raylib.GetTime();
            double frameTime = end - start;

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);

            simulation.Draw();

            // draw frame time in bottom right corner
            string frameTimeText = $"Update Frame Time: {frameTime * 1000.0:F2} ms";
            float textWidth = Raylib.MeasureText(frameTimeText, 20);
            int windowWidth = Raylib.GetScreenWidth();
            int windowHeight = Raylib.GetScreenHeight();
            Raylib.DrawText(frameTimeText, windowWidth - (int)textWidth - 8, windowHeight - 28, 20, Color.Black);

            Raylib.EndDrawing();
        }

        s_shouldCloseInput = true;
        Raylib.CloseWindow();
        return 0;
    }

    static void ProcessInput(string input, Simulation simulation)
    {
        input = input.Trim();
        var inputs = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        string command = inputs[0];
        string[] argList = inputs.Skip(1).ToArray();

        simulation.ProcessCommand(command, argList);
    }

    static void InputThread()
    {
        lock (s_consoleLock)
        {
            RedrawInputLine();
        }

        while (!s_shouldCloseInput)
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
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
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
