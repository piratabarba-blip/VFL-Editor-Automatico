using System.Runtime.InteropServices;

namespace VFL.EditorAutomatico;

internal static class Program
{
    private const string AppUserModelId = "VFL.EditorAutomatico";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [STAThread]
    private static void Main()
    {
        SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
