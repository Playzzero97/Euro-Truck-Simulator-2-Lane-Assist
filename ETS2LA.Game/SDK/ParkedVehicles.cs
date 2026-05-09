using ETS2LA.Shared;
using ETS2LA.Logging;
using ETS2LA.Backend.Events;

using System.Numerics;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using TruckLib;

namespace ETS2LA.Game.SDK;

public class ParkedVehicle
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 size;
    public int id;
    public bool isTrailer;

    public List<Vector3> GetCornersOnGround()
    {
        List<Vector3> corners = new List<Vector3>();
        Vector3 halfSize = size / 2;

        corners.Add(position + new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z));
        corners.Add(position + new Vector3(halfSize.X, -halfSize.Y, -halfSize.Z));
        corners.Add(position + new Vector3(halfSize.X, -halfSize.Y, halfSize.Z));
        corners.Add(position + new Vector3(-halfSize.X, -halfSize.Y, halfSize.Z));

        Quaternion invQuat = Quaternion.Conjugate(rotation);
        Vector3 euler = invQuat.ToEuler();
        Quaternion filteredRot = Quaternion.CreateFromYawPitchRoll(-euler.Y + (float)Math.PI, -euler.Z + (float)Math.PI, -euler.X);
        for (int i = 0; i < corners.Count; i++)
        {
            corners[i] = Vector3.Transform(corners[i] - position, filteredRot) + position;
        }

        return corners;
    }
}

public class ParkedVehicleData
{
    public required List<ParkedVehicle> vehicles;
}

public class ParkedVehiclesProvider
{
    private static readonly Lazy<ParkedVehiclesProvider> _instance = new(() => new ParkedVehiclesProvider());
    public static ParkedVehiclesProvider Current => _instance.Value;

    private float UpdateRate { get; set; } = 1f / 60f;
    public string EventString = "ETS2LA.Game.SDK.ParkedVehicles.Data";

    private MemoryReader? _reader;
    private ParkedVehicleData? _currentData;

    string mmapName = "Local\\ETS2LAParkedVehicles";
    string mmapNameLinux = "/dev/shm/ETS2LAParkedVehicles";
    int mmapSize = 1720;

    public ParkedVehiclesProvider()
    {
        Thread updateThread = new Thread(UpdateThread)
        {
            IsBackground = true
        };
        updateThread.Start();
    }

    private void UpdateThread()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        while (true)
        {
            int timeLeft = (int)((UpdateRate * 1000) - stopwatch.Elapsed.TotalMilliseconds);
            if (timeLeft > 0)
            {
                Thread.Sleep(timeLeft);
                continue;
            }

            stopwatch.Restart();
            try { Update(); }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "Error in camera update loop.");
            }
        }
    }
    
    private void Update()
    {
        if (_currentData == null)
        {
            _currentData = new ParkedVehicleData{ vehicles = new List<ParkedVehicle>() };
        }

        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? accessor = null;
        byte[] buffer = new byte[mmapSize];

        try
        {
            #if WINDOWS
                mmf = MemoryMappedFile.OpenExisting(mmapName);
            # else
                mmf = MemoryMappedFile.CreateFromFile(mmapNameLinux);
            # endif

            accessor = mmf.CreateViewAccessor(0, mmapSize, MemoryMappedFileAccess.Read);
            accessor.ReadArray(0, buffer, 0, mmapSize);
            _reader = new MemoryReader(buffer);
        }
        catch (FileNotFoundException)
        {
            Thread.Sleep(10000);
            _reader = null;
            return;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error initializing memory mapped file: {ex.Message}");
            Thread.Sleep(10000);
            _reader = null;
            return;
        }
        finally
        {
            accessor?.Dispose();
            mmf?.Dispose();
        }

        List<ParkedVehicle> vehicles = new List<ParkedVehicle>();
        int offset = 0;
        for (int i = 0; i < 40; i++)
        {
            ParkedVehicle vehicle = new ParkedVehicle();
            vehicle.position = new Vector3(
                _reader.ReadFloat(offset),
                _reader.ReadFloat(offset + 4),
                _reader.ReadFloat(offset + 8)
            ); offset += 12;
            vehicle.rotation = new Quaternion(
                _reader.ReadFloat(offset),
                _reader.ReadFloat(offset + 4),
                _reader.ReadFloat(offset + 8),
                _reader.ReadFloat(offset + 12)
            ); offset += 16;
            vehicle.size = new Vector3(
                _reader.ReadFloat(offset),
                _reader.ReadFloat(offset + 4),
                _reader.ReadFloat(offset + 8)
            ); offset += 12;
            vehicle.id = _reader.ReadShort(offset); offset += 2;
            vehicle.isTrailer = _reader.ReadBool(offset); offset += 1;

            vehicles.Add(vehicle);
        }

        _currentData.vehicles = vehicles;
        Events.Current.Publish<ParkedVehicleData>(EventString, _currentData);
    }
}