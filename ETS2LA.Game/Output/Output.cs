using ETS2LA.Logging;
using ETS2LA.Backend.Events;

using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Avalonia.Controls.Embedding.Offscreen;
using System.Runtime.InteropServices;

namespace ETS2LA.Game.Output;

public class GameOutput
{
    private static readonly Lazy<GameOutput> _instance = new(() => new GameOutput());
    public static GameOutput Current => _instance.Value;
    public string EventString = "ETS2LA.Game.Output.ControlEvent";

    public Dictionary<string, ControlChannel> Channels = new Dictionary<string, ControlChannel>();

    // Example:
    // "steering" : [(0.5, 1.0), (0.2, 0.5)]
    // Where the first value is the weight and the second value is the control value.
    // The channel itself doesn't matter, just their weights.
    private Dictionary<string, List<Tuple<float, float>>> curFrameFloats = new Dictionary<string, List<Tuple<float, float>>>();
    private float TickRate = 1f / 60f;

    private Stopwatch SinceTriedMemoryAccess = new Stopwatch();
    private bool MemoryAccessAvailable =>
    #if MACOSX
        legacyAccessor != null;
    #else
        legacyAccessor != null && modernAccessor != null;
    #endif
    private bool IsReset = false;

    // Legacy uses a virtual controller provided through the SCSControls plugin. This
    // system works the same as SteamInput for example.

    string legacyMapName = "Local\\SCSControls";
    string legacyMapNameLinux = "/dev/shm/SCS/SCSControls";
    string legacyMapNameMacOS = "/private/tmp/SCS/SCSControls";
    int legacyMapSize = 0;
    Dictionary<string, int> legacyShmOffsets = new Dictionary<string, int>();
    MemoryMappedFile? legacyMmf = null;
    MemoryMappedViewAccessor? legacyAccessor = null;
    private IntPtr _modernPtr = IntPtr.Zero;

    // Modern uses ETS2LAPlugin to write directly to the game's memory. This does
    // not however work on all devices, and it doesn't provide such an extensive list
    // of controls as the legacy system does.

    string modernMapName = "Local\\ETS2LAPluginInput";
    string modernMapNameLinux = "/dev/shm/ETS2LAPluginInput";
    string modernMapNameMacOS = "/ETS2LAPluginInput";
    int modernMapSize = 26;
    MemoryMappedFile? modernMmf = null;
    MemoryMappedViewAccessor? modernAccessor = null;

    #if MACOSX
    static class MacOutputShm
    {
        const int O_RDWR = 2;
        const int PROT_READ = 1, PROT_WRITE = 2;
        const int MAP_SHARED = 0x0001;

        [DllImport("libSystem.B.dylib", SetLastError = true)]
        static extern int shm_open(string name, int oflag, int mode);

        [DllImport("libSystem.B.dylib", SetLastError = true)]
        static extern IntPtr mmap(IntPtr addr, ulong length, int prot, int flags, int fd, long offset);

        [DllImport("libSystem.B.dylib")]
        static extern int close(int fd);

        public static IntPtr Open(string name, int size)
        {
            int fd = shm_open(name, O_RDWR, 0);
            if (fd < 0) throw new Exception($"shm_open failed for {name}, errno={Marshal.GetLastWin32Error()}");
            IntPtr ptr = mmap(IntPtr.Zero, (ulong)size, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
            close(fd);
            if (ptr == (IntPtr)(-1)) throw new Exception("mmap failed");
            return ptr;
        }
    }
    #endif

    public GameOutput()
    {
        // Calculate the memory offsets for each of the variables
        // for legacy mode.
        int boolSize = sizeof(bool);
        int floatSize = sizeof(float);

        int offset = 0;
        foreach (var field in typeof(ControlVariables).GetFields())
        {
            legacyShmOffsets[field.Name] = offset;
            if (field.FieldType == typeof(bool?))
                offset += boolSize;
            else if (field.FieldType == typeof(float?))
                offset += floatSize;
        }
        legacyMapSize = offset;

        SinceTriedMemoryAccess.Start();
        TryOpenMemory();
        // Then we start listening to events and spin up the update thread.
        Events.Current.Subscribe<ControlEvent>(EventString, OnControlEvent);
        Task.Run(Tick);
    }

    private void TryOpenMemory()
    {
        if (MemoryAccessAvailable)
            return;

        float secondsSinceLastTry = (float)SinceTriedMemoryAccess.Elapsed.TotalSeconds;
        if (secondsSinceLastTry < 5f)
            return;

        try
        {
        #if WINDOWS
            legacyMmf = MemoryMappedFile.OpenExisting(legacyMapName);
            modernMmf = MemoryMappedFile.OpenExisting(modernMapName);
            legacyAccessor = legacyMmf.CreateViewAccessor(0, legacyMapSize, MemoryMappedFileAccess.Write);
            modernAccessor = modernMmf.CreateViewAccessor(0, modernMapSize, MemoryMappedFileAccess.ReadWrite);
       #elif MACOSX
            Logging.Logger.Debug($"Trying to open legacy: {legacyMapNameMacOS}");
            legacyMmf = MemoryMappedFile.CreateFromFile(legacyMapNameMacOS, FileMode.Open, null, 0, MemoryMappedFileAccess.ReadWrite);
            Logging.Logger.Debug("legacyMmf opened");
            legacyAccessor = legacyMmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Write);
            Logging.Logger.Debug($"legacyAccessor: {legacyAccessor != null}");
            try
            {
                _modernPtr = MacOutputShm.Open(modernMapNameMacOS, modernMapSize);
                Logging.Logger.Debug($"modernPtr: {_modernPtr}");
            }
            catch
            {
                Logging.Logger.Debug("Modern output not available, continuing with legacy only.");
            }
        #else
            legacyMmf = MemoryMappedFile.CreateFromFile(legacyMapNameLinux);
            modernMmf = MemoryMappedFile.CreateFromFile(modernMapNameLinux);
            legacyAccessor = legacyMmf.CreateViewAccessor(0, legacyMapSize, MemoryMappedFileAccess.Write);
            modernAccessor = modernMmf.CreateViewAccessor(0, modernMapSize, MemoryMappedFileAccess.ReadWrite);
        #endif
        }
        
        catch 
        {
            //Logging.Logger.Error("Failed to open memory: " + ex.Message);
            #if MACOSX
            legacyMmf?.Dispose();
            legacyMmf = null;
            legacyAccessor = null;
            #else
            legacyAccessor = null;
            modernAccessor = null;
            #endif
            _modernPtr = IntPtr.Zero;
        }

        Logging.Logger.Debug(MemoryAccessAvailable ? "Successfully opened memory for output." 
                                                  : "Memory not available for output.");
        SinceTriedMemoryAccess.Restart();
    }

    public void OnControlEvent(ControlEvent controlEvent)
    {
        string channel = controlEvent.ChannelDefinition.Id;
        if (!Channels.ContainsKey(channel))
        {
            Channels[channel] = new ControlChannel{
                Definition = controlEvent.ChannelDefinition,
                Properties = controlEvent.Properties,
                Variables = controlEvent.Variables
            };
        }
        else if (controlEvent.Variables == null || controlEvent.Properties == null)
        {
            Channels.Remove(channel);
        }
        else
        {
            Channels[channel].Properties = controlEvent.Properties;
            Channels[channel].Variables = controlEvent.Variables;
            Channels[channel].LastUpdate.Restart();
            Channels[channel].BoolsProcessed = false;
        }
    }

    private void ResetOutputs()
    {        
        if (!MemoryAccessAvailable)
            return;

        if (IsReset)
            return;
        
        if (legacyAccessor != null)
        {
            foreach (var field in typeof(ControlVariables).GetFields())
            {
                if (field.FieldType == typeof(float?))
                {
                    WriteFloat(legacyAccessor!, legacyShmOffsets[field.Name], 0);
                }
                else if (field.FieldType == typeof(bool?))
                {
                    WriteBool(legacyAccessor!, legacyShmOffsets[field.Name], false);
                }
            }
            legacyAccessor.Flush();
        }

       #if MACOSX
        if (_modernPtr != IntPtr.Zero)
        {
            WriteFloatPtr(_modernPtr, 0, 0);
            WriteBoolPtr(_modernPtr, 4, false);
            WriteDoublePtr(_modernPtr, 5, 0);
            WriteFloatPtr(_modernPtr, 13, 0);
            WriteBoolPtr(_modernPtr, 17, false);
            WriteDoublePtr(_modernPtr, 18, 0);
        }
#else
        if (modernAccessor != null)
        {
            WriteFloat(modernAccessor, 0, 0);
            WriteBool(modernAccessor, 4, false);
            WriteDouble(modernAccessor, 5, 0);
            WriteFloat(modernAccessor, 13, 0);
            WriteBool(modernAccessor, 17, false);
            WriteDouble(modernAccessor, 18, 0);
            modernAccessor.Flush();
        }
#endif

        IsReset = true;
        Logging.Logger.Debug("Reset outputs to default values.");
    }

    private void ToggleBool(MemoryMappedViewAccessor accessor, int offset)
    {
        WriteBool(accessor, offset, true);
        Thread.Sleep(50);
        WriteBool(accessor, offset, false);
    }

    private void WriteBool(MemoryMappedViewAccessor accessor, int offset, bool value)
    {
        accessor.Write(offset, value);
    }

    private void WriteFloat(MemoryMappedViewAccessor accessor, int offset, float value)
    {
        accessor.Write(offset, value);
    }

    private void WriteDouble(MemoryMappedViewAccessor accessor, int offset, double value)
    {
        accessor.Write(offset, value);
    }

    private void ProcessChannel(ControlChannel channel)
    {
        var boolType = channel.Properties.BooleanType;
        var weight = channel.Properties.Weight;

        foreach (var prop in typeof(ControlVariables).GetFields())
        {
            var value = prop.GetValue(channel.Variables);
            if (value != null)
            {
                if (prop.FieldType == typeof(bool?) && !channel.BoolsProcessed)
                {
                    bool boolValue = (bool)value;
                    if(boolValue && boolType == ControlBooleanType.TrueToToggle)
                        Task.Run(() => ToggleBool(legacyAccessor!, legacyShmOffsets[prop.Name]));
                    else
                        WriteBool(legacyAccessor!, legacyShmOffsets[prop.Name], boolValue);
                }

                if (prop.FieldType == typeof(float?))
                {
                    float floatValue = (float)value;

                    string propName = prop.Name;
                    if (propName == "aforward" || propName == "abackward")
                        propName = "acceleration";

                    if (!curFrameFloats.ContainsKey(propName))
                        curFrameFloats[propName] = new List<Tuple<float, float>>();

                    curFrameFloats[propName].Add(new Tuple<float, float>(weight, floatValue));
                }
            }
        }

        channel.BoolsProcessed = true;
    }

    private void WriteFloatPtr(IntPtr ptr, int offset, float value)
    {
        Marshal.WriteInt32(ptr + offset, BitConverter.SingleToInt32Bits(value));
    }

    private void WriteBoolPtr(IntPtr ptr, int offset, bool value)
    {
        Marshal.WriteByte(ptr + offset, value ? (byte)1 : (byte)0);
    }

    private void WriteDoublePtr(IntPtr ptr, int offset, double value)
    {
        Marshal.WriteInt64(ptr + offset, BitConverter.DoubleToInt64Bits(value));
    }

    public void Tick()
    {
        Stopwatch tickTimer = Stopwatch.StartNew();
        while(true)
        {
            double timeLeft = TickRate - tickTimer.Elapsed.TotalSeconds;
            if (timeLeft > 0 && timeLeft < TickRate)
            {
                if (timeLeft * 1000 > 0.5)
                {
                    Thread.Sleep((int)(timeLeft * 1000));
                    continue;
                }
            }

            // These || need to be added to silence warnings...
            // If someone knows how to make the compiler understand that MemoryAccessAvailable ensures
            // that the accessors are not null, then please tell me.
            #if MACOSX
                if (!MemoryAccessAvailable || legacyAccessor == null)
            #else
                if (!MemoryAccessAvailable || legacyAccessor == null || modernAccessor == null)
            #endif
            {
                TryOpenMemory();
                tickTimer.Restart();
                continue;
            }

            if(Channels.Count == 0)
            {
                if (!IsReset)
                    ResetOutputs();
                
                tickTimer.Restart();
                continue;
            }

            IsReset = false;
            try
            {
                foreach (var channel in Channels.Values)
                {
                    if (channel.Properties == null || channel.Variables == null)
                        continue;

                    if (channel.LastUpdate.Elapsed.TotalSeconds > channel.Definition.Timeout)
                    {
                        Channels.Remove(channel.Definition.Id);
                        continue;
                    }

                    ProcessChannel(channel);
                }
            } catch {}

            double time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            foreach (var kvp in curFrameFloats)
            {
                string propName = kvp.Key;
                List<Tuple<float, float>> values = kvp.Value;

                float totalWeight = values.Sum(v => v.Item1);
                float weightedValue = values.Sum(v => v.Item1 * v.Item2) / totalWeight;
                weightedValue = Math.Clamp(weightedValue, -1f, 1f);


                if (propName == "steering")
                {
                    #if MACOSX
                        if (_modernPtr != IntPtr.Zero)
                        {
                            WriteFloatPtr(_modernPtr, 0, weightedValue);
                            WriteBoolPtr(_modernPtr, 4, weightedValue != 0.0f);
                            WriteDoublePtr(_modernPtr, 5, time);
                        }
                    #else
                        WriteFloat(modernAccessor, 0, weightedValue);
                        WriteBool(modernAccessor, 4, weightedValue != 0.0f);
                        WriteDouble(modernAccessor, 5, time);
                    #endif
                        WriteFloat(legacyAccessor, legacyShmOffsets[propName], weightedValue);
                }
                else if (propName == "acceleration")
                {
                    // TODO: Fix acceleration via the new accessor (any acceleration is max?)
                    // WriteFloat(modernAccessor, 13, weightedValue);
                    // WriteBool(modernAccessor, 17, weightedValue != 0.0f);
                    // WriteDouble(modernAccessor, 18, time);
                    #if MACOSX
                        if (_modernPtr != IntPtr.Zero)
                        {
                            WriteFloatPtr(_modernPtr, 13, weightedValue);
                            WriteBoolPtr(_modernPtr, 17, weightedValue != 0.0f);
                            WriteDoublePtr(_modernPtr, 18, time);
                        }
                    #else
                        WriteFloat(modernAccessor, 13, weightedValue);
                        WriteBool(modernAccessor, 17, weightedValue != 0.0f);
                        WriteDouble(modernAccessor, 18, time);
                    #endif
                }
                Logging.Logger.Debug($"steering={weightedValue} ptr={_modernPtr}");
                // or for accel
                Logging.Logger.Debug($"accel={weightedValue} ptr={_modernPtr}");
            }

            #if MACOSX
                legacyAccessor.Flush();
            #else
                modernAccessor.Flush();
                legacyAccessor.Flush();
            #endif

            curFrameFloats.Clear();
            tickTimer.Restart();
        }

    }
}