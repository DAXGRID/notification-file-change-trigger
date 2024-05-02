using System.Diagnostics;

namespace NotificationFileChangeTrigger;

internal static class Trigger
{
    public static bool Execute(string command, string fileName, Action<string> logInformation, Action<string> logError)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                EnvironmentVariables = { { "TRIGGER_FILE_NAME", fileName } },
            }
        };

        // Read stdout and stderr asynchronously
        proc.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                logInformation(e.Data);
            }
        };

        proc.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                logError(e.Data);
            }
        };

        proc.Start();

        // Begin asynchronously reading stdout and stderr
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        proc.WaitForExit();

        return proc.ExitCode switch
        {
            not 0 => false,
            _ => true
        };
    }
}
