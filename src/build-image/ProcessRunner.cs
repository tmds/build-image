using System.Diagnostics;
using System.Text;

static class ProcessRunner
{
    public static readonly Action<string?> WriteToConsoleError =
        (s) =>
        {
            if (s is not null)
            {
                Console.Error.WriteLine(s);
            }
        };

    public static readonly Action<string?> WriteToConsoleOut =
        (s) =>
        {
            if (s is not null)
            {
                Console.WriteLine(s);
            }
        };

    public static readonly Action<string?> Ignore =
        (s) => {};

    public static Action<string?> WriteTo(StringBuilder sb, bool trimEnd = true)
    {
        return (s) =>
        {
            if (s is not null)
            {
                sb.AppendLine(s);
            }
            else
            {
                if (trimEnd)
                {
                    int i;
                    for (i = sb.Length - 1; i >= 0; i--)
                        if (!char.IsWhiteSpace(sb[i]))
                            break;

                    if (i < sb.Length - 1)
                        sb.Length = i + 1;
                }
            }
        };
    }

    public static int Run(string filename, IEnumerable<string> arguments, StringBuilder? stdout = null, StringBuilder? stderr = null, IEnumerable<KeyValuePair<string, string>>? environmentVariables = null)
        => Run(filename, arguments, stdout is null ? Ignore : WriteTo(stdout), stderr is null ? Ignore : WriteTo(stderr), environmentVariables);

    public static int Run(string filename, IEnumerable<string> arguments, Action<string?> onStdout, Action<string?> onStderr, IEnumerable<KeyValuePair<string, string>>? environmentVariables = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = filename,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        if (environmentVariables != null)
        {
            foreach (var env in environmentVariables)
            {
                psi.Environment[env.Key] = env.Value;
            }
        }

        using var process = Process.Start(psi)!;

        process.ErrorDataReceived += (sender, e) =>
        {
            onStderr?.Invoke(e.Data);
        };

        process.OutputDataReceived += (sender, e) =>
        {
            onStdout?.Invoke(e.Data);
        };

        process.StandardInput.Close();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        process.WaitForExit();

        return process.ExitCode;
    }
}