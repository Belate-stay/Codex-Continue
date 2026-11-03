using System.Diagnostics;
using System.IO;
using System.Reflection;
using CodexContinue;

static class SingleInstanceChecks
{
    public static int Probe(string[] args)
    {
        using var guard = Create(args[1], () => { });
        bool primary = Primary(guard);
        if (!primary) Request(guard);
        Console.WriteLine(primary ? "primary" : "secondary");
        Console.Out.Flush();
        if (primary && args.Length > 2 && args[2] == "hold") Thread.Sleep(Timeout.Infinite);
        return 0;
    }

    public static void DuplicateLaunches()
    {
        string name = Name();
        int activations = 0;
        using (var owner = Create(name, () => Interlocked.Increment(ref activations)))
        {
            Require(Primary(owner), "The first process does not own the single-instance guard");
            var children = Enumerable.Range(0, 8).Select(_ => Start(name)).ToArray();
            try
            {
                foreach (var child in children)
                {
                    Require(child.WaitForExit(7000), "A duplicate launch does not exit promptly");
                    string result = child.StandardOutput.ReadToEnd().Trim();
                    Require(child.ExitCode == 0 && result == "secondary",
                        "A duplicate created another primary: " + result + " " + child.StandardError.ReadToEnd());
                }
                Require(SpinWait.SpinUntil(() => Volatile.Read(ref activations) > 0, 2000),
                    "Duplicate launches do not notify the existing instance");
                Require(Primary(owner), "Duplicate launches displace the primary instance");
            }
            finally { foreach (var child in children) { Stop(child); child.Dispose(); } }
        }
        using var next = Create(name, () => { });
        Require(Primary(next), "Normal exit does not release the single-instance guard");
    }

    public static void ConcurrentAndCrashRecovery()
    {
        string name = Name();
        var contenders = Enumerable.Range(0, 6).Select(_ => Start(name, hold: true)).ToArray();
        try
        {
            var results = contenders.Select(ReadReady).ToArray();
            Require(results.Count(r => r == "primary") == 1 && results.Count(r => r == "secondary") == 5,
                "Simultaneous launches do not elect exactly one primary");
            using var observer = Create(name, () => { });
            Require(!Primary(observer), "The active owner does not exclude another process");
            foreach (var child in contenders) Stop(child);
            using var recovered = Create(name, () => { });
            Require(Primary(recovered), "An abandoned mutex prevents restart after the owner crashes");
        }
        finally { foreach (var child in contenders) { Stop(child); child.Dispose(); } }
    }

    static string ReadReady(Process process) => process.StandardOutput.ReadLineAsync()
        .WaitAsync(TimeSpan.FromSeconds(7)).GetAwaiter().GetResult() ?? "";
    static string Name() => @"Local\CodexContinue.Tests." + Guid.NewGuid().ToString("N");
    static IDisposable Create(string name, Action activate)
    {
        var type = typeof(MainWindow).Assembly.GetType("CodexContinue.SingleInstance")
            ?? throw new Exception("Missing single-instance startup guard");
        return (IDisposable)Activator.CreateInstance(type, name, activate)!;
    }
    static bool Primary(object guard) => (bool)guard.GetType().GetProperty("IsPrimary")!.GetValue(guard)!;
    static void Request(object guard) => guard.GetType().GetMethod("RequestActivation")!.Invoke(guard, null);
    static Process Start(string name, bool hold = false)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--single-instance-probe");
        start.ArgumentList.Add(name);
        if (hold) start.ArgumentList.Add("hold");
        return Process.Start(start)!;
    }
    static void Stop(Process process)
    {
        if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(3000); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
