using System.Diagnostics;

namespace NotificationFileChangeTrigger;

internal static class Trigger
{
    public static (bool success, string message, string? errorMessage) Execute(string command, string fileName)
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
                EnvironmentVariables = { { "TRIGGER_FILE_NAME", fileName } }
            }
        };

        proc.Start();
        proc.WaitForExit();

        return proc.ExitCode switch
        {
            not 0 => (false, proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd()),
            _ => (true, proc.StandardOutput.ReadToEnd(), null)
        };
    }
}
