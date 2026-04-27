namespace CodexAlertSetup;

public static class SafeFilePicker
{
    public static Task<string?> PickFileAsync(string title, string filter, string currentPath)
    {
        return RunStaAsync(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false,
                RestoreDirectory = true,
                AutoUpgradeEnabled = false
            };

            var initialDirectory = ResolveInitialDirectory(currentPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            if (File.Exists(currentPath))
            {
                dialog.FileName = Path.GetFileName(currentPath);
            }

            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        });
    }

    public static Task<string?> PickFolderAsync(string description, string currentPath)
    {
        return RunStaAsync(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = description,
                ShowNewFolderButton = true,
                AutoUpgradeEnabled = false
            };

            var initialDirectory = ResolveInitialDirectory(currentPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.SelectedPath = initialDirectory;
            }

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        });
    }

    private static Task<string?> RunStaAsync(Func<string?> action)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Application.OleRequired();
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Codex Alert file picker"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string ResolveInitialDirectory(string path)
    {
        try
        {
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (File.Exists(path))
            {
                return Path.GetDirectoryName(path) ?? "";
            }

            if (Directory.Exists(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }
        }
        catch
        {
            // Fall through to Documents when the current text is not a valid path yet.
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
