using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Dev;

class Program
{
    static void Main(string[] args)
    {
        var nativeSettings = new NativeWindowSettings
        {
            Title = "Rocket Engine Dev Test",
            WindowState = WindowState.Fullscreen
        };
        using var game = new Game(GameWindowSettings.Default, nativeSettings);
        game.Run();
    }
}