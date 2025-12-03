using Raylib_cs;

namespace WarpSimulation;

internal static class Program
{
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

        while (!Raylib.WindowShouldClose())
        {
            float delta = Raylib.GetFrameTime();

            simulation.Update(delta);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.White);
            simulation.Draw();
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
