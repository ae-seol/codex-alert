using System.Diagnostics;

namespace CodexAlertSetup;

internal static class ShellLauncher
{
    public static void OpenPath(string path, Action<Exception>? onError = null)
    {
        Open(Path.GetFullPath(path), onError);
    }

    public static void Open(string target, Action<Exception>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            onError?.Invoke(new ArgumentException("Nothing to open.", nameof(target)));
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                using var _ = Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception);
            }
        });
    }
}
