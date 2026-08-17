namespace GDG.VirtualPrinter.Core;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class LockFile : IDisposable
{
    private readonly string _path;
    private bool _ownsLock;
    private bool _released;
    private bool _disposed;

    public LockFile(string path) => _path = path;

    public async Task<bool> ExecuteAsync(Func<Task<bool>> action, Func<Task> release)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await AcquireAsync();

        var ok = await action();
        if (!ok) return false;

        await release();
        _released = true;
        return true;
    }

    private async Task AcquireAsync()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Same serialization model as Cube DesktopBridge: one bridge hand-off
        // at a time. Stale lock is removed after timeout.
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (File.Exists(_path))
        {
            if (DateTime.UtcNow >= deadline)
            {
                try { File.Delete(_path); } catch { }
                break;
            }
            await Task.Delay(100);
        }

        var temp = _path + "." + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, "lock");
        File.Move(temp, _path, true);
        _ownsLock = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsLock && !_released)
        {
            try { File.Delete(_path); } catch { }
        }
    }
}
