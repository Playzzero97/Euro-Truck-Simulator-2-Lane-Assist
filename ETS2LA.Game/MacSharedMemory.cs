using System.IO;
using System.Runtime.InteropServices;

namespace ETS2LA.Game;

internal sealed class MacSharedMemory : IDisposable
{
    private const int O_RDONLY = 0;
    private const int PROT_READ = 1;
    private const int MAP_SHARED = 1;

    private static readonly IntPtr MapFailed = new(-1);

    [DllImport("libc", EntryPoint = "shm_open", SetLastError = true)]
    private static extern int ShmOpen(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int oflag);

    [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
    private static extern IntPtr MMap(
        IntPtr address,
        UIntPtr length,
        int protection,
        int flags,
        int fd,
        long offset);

    [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
    private static extern int Munmap(IntPtr address, UIntPtr length);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int fd);

    private IntPtr address;
    private readonly UIntPtr length;

    private MacSharedMemory(IntPtr address, UIntPtr length)
    {
        this.address = address;
        this.length = length;
    }

    public static MacSharedMemory Open(string name, int size)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException();

        string shmName = name.StartsWith("/") ? name : "/" + name;
        int fd = ShmOpen(shmName, O_RDONLY);

        if (fd < 0)
        {
            int error = Marshal.GetLastPInvokeError();

            if (error == 2)
                throw new FileNotFoundException(
                    $"shm_open('{shmName}') failed, errno {error}");

            throw new IOException(
                $"shm_open('{shmName}') failed, errno {error}");
        }

        UIntPtr mapLength = new((uint)size);
        IntPtr mappedAddress;

        try
        {
            mappedAddress = MMap(
                IntPtr.Zero,
                mapLength,
                PROT_READ,
                MAP_SHARED,
                fd,
                0);
        }
        finally
        {
            Close(fd);
        }

        if (mappedAddress == MapFailed)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException(
                $"mmap('{shmName}') failed, errno {error}");
        }

        return new MacSharedMemory(mappedAddress, mapLength);
    }

    public void Read(byte[] buffer)
    {
        if (address == IntPtr.Zero || address == MapFailed)
            throw new ObjectDisposedException(nameof(MacSharedMemory));

        if ((ulong)buffer.Length > length.ToUInt64())
            throw new ArgumentException("Buffer exceeds mapped memory size.");

        Marshal.Copy(address, buffer, 0, buffer.Length);
    }

    public void Dispose()
    {
        if (address == IntPtr.Zero || address == MapFailed)
            return;

        Munmap(address, length);
        address = IntPtr.Zero;
    }
}