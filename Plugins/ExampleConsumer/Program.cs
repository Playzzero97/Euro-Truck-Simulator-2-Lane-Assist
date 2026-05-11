using ETS2LA.Notifications;
using ETS2LA.Shared;
using ETS2LA.Telemetry;
using ETS2LA.Backend.Events;
using ETS2LA.Game.SDK;
using ETS2LA.Overlay;
using ETS2LA.Overlay.AR;

using System.Numerics;
using Hexa.NET.ImGui;

namespace ExampleConsumer;

public class MyConsumer : Plugin
{
    public override float TickRate => 60f;
    public override PluginInformation Info => new PluginInformation
    {
        Name = "Example Consumer",
        Description = "An example data consumer plugin.",
        AuthorName = "Tumppi066",
    };

    private TrafficData? _trafficData;
    private ParkedVehicleData? _parkedVehicleData;

    public override void Init()
    {
        base.Init();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        Events.Current.Subscribe<float>("ExampleProvider.Time", OnTimeReceived);
        Events.Current.Subscribe<GameTelemetryData>(GameTelemetry.Current.EventString, OnGameTelemetryReceived);
        Events.Current.Subscribe<CameraData>(CameraProvider.Current.EventString, OnCameraReceived);
        Events.Current.Subscribe<TrafficData>(TrafficProvider.Current.EventString, OnTrafficReceived);
        Events.Current.Subscribe<SemaphoreData>(SemaphoreProvider.Current.EventString, OnSemaphoreReceived);
        Events.Current.Subscribe<NavigationData>(NavigationProvider.Current.EventString, OnNavigationReceived);
        Events.Current.Subscribe<ParkedVehicleData>(ParkedVehiclesProvider.Current.EventString, OnParkedVehicleReceived);

        OverlayHandler.Current.AR.RegisterRenderCallback(new ARRenderCallback
        {
            Definition = new ARRendererDefinition
            {
                Name = "Example AR Renderer"
            },
            Render3D = () =>
            {
                ARRenderer AR = OverlayHandler.Current.AR;
                // Test lines
                AR.Draw3DLine(new ARCoordinate(Vector3.Zero, ARCoordinateCenter.Truck), new ARCoordinate(new Vector3(0, 0, 1), ARCoordinateCenter.Truck), 0xFF0000FF);
                AR.Draw3DLine(new ARCoordinate(Vector3.Zero, ARCoordinateCenter.Truck), new ARCoordinate(new Vector3(1, 0, 0), ARCoordinateCenter.Truck), 0x00FF00FF);
                AR.Draw3DLine(new ARCoordinate(Vector3.Zero, ARCoordinateCenter.Truck), new ARCoordinate(new Vector3(0, 1, 0), ARCoordinateCenter.Truck), 0x0000FFFF);

                // Test window
                AR.BeginWindow("Example Window", forceHeight: 120, forceWidth: 300);
                ImGui.Text($"Position: ({position.X:F0}, {position.Y:F0}, {position.Z:F0})");
                ImGui.Text($"Speed: {speed*3.6:F1} km/h");
                AR.EndWindow(new ARCoordinate(0,0,0, ARCoordinateCenter.Truck), Quaternion.CreateFromYawPitchRoll(0, 0, 0), 4);

                if (_trafficData != null)
                {
                    foreach (var vehicle in _trafficData.vehicles)
                    {
                        var bottomCorners = vehicle.GetCornersOnGround();
                        var topCorners = new List<Vector3>
                        {
                            bottomCorners[0] + new Vector3(0, vehicle.size.Y, 0),
                            bottomCorners[1] + new Vector3(0, vehicle.size.Y, 0),
                            bottomCorners[2] + new Vector3(0, vehicle.size.Y, 0),
                            bottomCorners[3] + new Vector3(0, vehicle.size.Y, 0)
                        };

                        AR.Draw3DQuad(bottomCorners[0], bottomCorners[1], bottomCorners[2], bottomCorners[3], 0xFFFFFF);
                        AR.Draw3DQuad(topCorners[0], topCorners[1], topCorners[2], topCorners[3], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[0], topCorners[0], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[1], topCorners[1], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[2], topCorners[2], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[3], topCorners[3], 0xFFFFFF);

                        foreach (var trailer in vehicle.trailers)
                        {
                            bottomCorners = trailer.GetCornersOnGround();
                            topCorners = new List<Vector3>
                            {
                                bottomCorners[0] + new Vector3(0, trailer.size.Y, 0),
                                bottomCorners[1] + new Vector3(0, trailer.size.Y, 0),
                                bottomCorners[2] + new Vector3(0, trailer.size.Y, 0),
                                bottomCorners[3] + new Vector3(0, trailer.size.Y, 0)
                            };

                            AR.Draw3DQuad(bottomCorners[0], bottomCorners[1], bottomCorners[2], bottomCorners[3], 0xFFFFFF);
                            AR.Draw3DQuad(topCorners[0], topCorners[1], topCorners[2], topCorners[3], 0xFFFFFF);
                            AR.Draw3DLine(bottomCorners[0], topCorners[0], 0xFFFFFF);
                            AR.Draw3DLine(bottomCorners[1], topCorners[1], 0xFFFFFF);
                            AR.Draw3DLine(bottomCorners[2], topCorners[2], 0xFFFFFF);
                            AR.Draw3DLine(bottomCorners[3], topCorners[3], 0xFFFFFF);
                        }
                    }
                }
                if (_parkedVehicleData != null)
                {
                    foreach (var vehicle in _parkedVehicleData.vehicles)
                    {
                        var bottomCorners = vehicle.GetCornersOnGround();
                        var topCorners = new List<Vector3>
                        {
                            bottomCorners[0] + new Vector3(0, vehicle.size.Y, 0),
                            bottomCorners[1] + new Vector3(0, vehicle.size.Y, 0),
                            bottomCorners[2] + new Vector3(0, vehicle.size.Y, 0),
                            bottomCorners[3] + new Vector3(0, vehicle.size.Y, 0)
                        };

                        AR.Draw3DQuad(bottomCorners[0], bottomCorners[1], bottomCorners[2], bottomCorners[3], 0xFFFFFF);
                        AR.Draw3DQuad(topCorners[0], topCorners[1], topCorners[2], topCorners[3], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[0], topCorners[0], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[1], topCorners[1], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[2], topCorners[2], 0xFFFFFF);
                        AR.Draw3DLine(bottomCorners[3], topCorners[3], 0xFFFFFF);
                    }
                }
            }
        });
    }

    public override void OnDisable()
    {
        base.OnDisable();
        NotificationHandler.Current.CloseNotification("ExampleConsumer.Speed");
        NotificationHandler.Current.CloseNotification("ExampleConsumer.RPM");
        OverlayHandler.Current.AR.UnregisterRenderCallback("Example AR Renderer");
    }

    private float output = 0;
    private float speed = 0;
    private float rpm = 0;
    private Vector3 position;

    public override void Tick()
    {
        // sine wave output from -1 to 1
        double time = DateTime.Now.TimeOfDay.TotalSeconds;
        output = (float)Math.Sin(time * 2 * Math.PI / 8);

        // NotificationHandler.Current.SendNotification(new Notification
        // {
        //     Id = "ExampleConsumer.Speed",
        //     Title = "Truck Speed",
        //     Content = $"{speed:F2} km/h",
        //     Level = NotificationLevel.Information,
        //     Progress = speed / (100 * 3.6f) * 100f,
        //     IsProgressIndeterminate = false,
        //     CloseAfter = 0 
        // });

        // NotificationHandler.Current.SendNotification(new Notification
        // {
        //     Id = "ExampleConsumer.RPM",
        //     Title = "Engine RPM",
        //     Content = $"{rpm:F0} RPM",
        //     Level = NotificationLevel.Information,
        //     Progress = rpm / 3000.0f * 100f,
        //     IsProgressIndeterminate = false,
        //     CloseAfter = 0 
        // });

        // Events.Current.Publish<float>("ForceFeedback.Output", output);
        NotificationHandler.Current.SendNotification(new Notification
        {
            Id = "ExampleConsumer.Output",
            Title = "Steering Output",
            Content = $"Output: {output:F2}",
            Level = NotificationLevel.Information,
            Progress = (output + 1.0f) / 2.0f * 100f,
            IsProgressIndeterminate = false,
            CloseAfter = 0 
        });

        // Events.Current.Publish(GameOutput.Current.EventString, new ControlEvent
        // {
        //     ChannelDefinition = new ControlChannelDefinition
        //     {
        //         Id = "ExampleConsumer.Steering",
        //     },
        //     Variables = new ControlVariables
        //     {
        //         steering = output
        //     }
        // });

        // SDKControlEvent controlEvent = new SDKControlEvent
        // {
        //     steering = output,
        //     light = true,
        //     hblight = false
        // };
        // Events.Current.Publish<SDKControlEvent>("ETS2LA.Output.Event", controlEvent);
    }

    private void OnTimeReceived(float data)
    {
        if (!_IsRunning)
            return;

        // Logger.Info($"MyConsumer received time: {data}");
        // Logger.Info($"Delay to receive data: {DateTime.Now.Microsecond - data} microseconds");
    }

    private void OnGameTelemetryReceived(GameTelemetryData data)
    {
        if (!_IsRunning)
            return;

        speed = data.truckFloat.speed;
        rpm = data.truckFloat.engineRpm;
        position = data.truckPlacement.coordinate.ToVector3();
    }

    private void OnCameraReceived(CameraData camera)
    {
        if (!_IsRunning)
            return;

        // Vector3 euler = camera.rotation.ToEuler();
        // Logger.Info($"MyConsumer received camera FOV: {camera.fov}, Position: ({camera.position.X}, {camera.position.Y}, {camera.position.Z}), Rotation: ({euler.X}, {euler.Y}, {euler.Z})");
    }

    private void OnTrafficReceived(TrafficData traffic)
    {
        if (!_IsRunning)
            return;

        _trafficData = traffic;
    }

    private void OnParkedVehicleReceived(ParkedVehicleData data)
    {
        if (!_IsRunning)
            return;

        _parkedVehicleData = data;
    }

    private void OnSemaphoreReceived(SemaphoreData data)
    {
        if (!_IsRunning)
            return;

        foreach (var semaphore in data.semaphores)
        {
            if (semaphore.type != SemaphoreType.TRAFFICLIGHT)
                continue;
            //Logger.Info($"Semaphore ID: {semaphore.id}, Type: {semaphore.type}, State: {semaphore.state}, Time Remaining: {semaphore.time_remaining}");
        }
    }

    private void OnNavigationReceived(NavigationData data)
    {
        if (!_IsRunning)
            return;

        // int valid = 0;
        // foreach (var entry in data.entries)
        // {
        //     if (entry.nodeUid != 0)
        //         valid++;
        // }
        // Logger.Info($"MyConsumer received {valid} valid navigation waypoints.");
    }
}
