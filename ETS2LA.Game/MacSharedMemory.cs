using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ETS2LA.Game;

/// <summary>
/// macOS has no /dev/shm, so POSIX shared memory made by the game plugins
/// (shm_open) can't be opened by path. This opens it by name and wraps it
/// in a normal MemoryMappedFile, so the rest of the code stays the same.
/// </summary>
internal static class MacSharedMemory
{
    // shm_open is variadic, the mode argument is left out on purpose since
    // it's only read with O_CREAT (and variadic args differ on Apple Silicon).
    [DllImport("libc", SetLastError = true)]
    private static extern int shm_open(string name, int oflag);

    public static MemoryMappedFile Open(string name, bool writable)
    {
        int fd = shm_open(name, writable ? 2 : 0); // O_RDWR : O_RDONLY
        if (fd < 0)
            throw new FileNotFoundException($"shm_open('{name}') failed, errno {Marshal.GetLastPInvokeError()}");

        return MemoryMappedFile.CreateFromFile(new SafeFileHandle(fd, true), null, 0,
            writable ? MemoryMappedFileAccess.ReadWrite : MemoryMappedFileAccess.Read,
            HandleInheritability.None, false);
    }
}
