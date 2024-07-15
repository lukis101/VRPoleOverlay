using System.Numerics;
using System.Diagnostics;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DJL.VRPoleOverlay
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal class Program
    {
        static private string executablePath;
        static private string settingsPath;
        static private JsonSerializerOptions serOptions = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        static private Configuration config;
        // These only need to be (re)calculated when reading config
        static private Vector3 translation = Vector3.Zero;
        static private Matrix4x4 scale = Matrix4x4.Identity;

        static void Main(string[] args)
        {
            string assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Console.WriteLine("VRPoleOverlay v"+ assemblyVersion);

            executablePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? "";

            // Settings
            config = new Configuration();
            settingsPath = Path.Combine(new string[] { executablePath, config.SETTINGS_FILENAME });
            LoadSettings();

            // OpenVR Setup
            var error = new ETrackedPropertyError();
            var initError = EVRInitError.Unknown;

            var ovrApplicationType = EVRApplicationType.VRApplication_Overlay;
            OpenVR.InitInternal(ref initError, ovrApplicationType);
            uint uncbVREvent = (uint)Marshal.SizeOf(typeof(VREvent_t));

            // Check state of VR runtime (in case running without headset)
            if ((OpenVR.Applications == null) || (OpenVR.Overlay == null))
            {
                Console.WriteLine("ERROR: SteamVR not fully initialized, exiting...");
                return;
            }

            // Optional auto-start
            bool firsttime = false;
            if (config.ASK_AUTOSTART)
            {
                if (!OpenVR.Applications.IsApplicationInstalled(config.APPLICATION_KEY))
                {
                    firsttime = true;
                    Console.WriteLine("Do you want to make this app auto-start with SteamVR?");
                    Console.WriteLine("[Y]es, [N]o, No and [D]on't ask again");
                    ConsoleKeyInfo key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.Escape:
                        case ConsoleKey.N:
                            break;
                        case ConsoleKey.Y:
                            OVRUtilities.SetupOpenVRAutostart(config);
                            break;
                        case ConsoleKey.D:
                            config.ASK_AUTOSTART = false;
                            SaveSettings();
                            break;
                        default:
                            break;
                    }
                } // else TODO: Check if registered under different path and ask to update?
            }

#if !DEBUG
            // Don't minimize when debugging or running first time
            if (!firsttime)
            {
                Console.WriteLine("Minimizing Window");
                WindowsUtilities.SetWindowState(WindowsUtilities.GetConsoleWindow(), WindowsUtilities.CMDSHOW.SW_MINIMIZE);
            }
#endif

            // Create the actual overlay
            ulong overlayHandle = 0;
            if (!OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.CreateOverlay(config.OVERLAY_KEY, config.OVERLAY_NAME, ref overlayHandle)))
            {
                Console.WriteLine("ERROR: Unable to create overlay, exiting...");
                return;
            }
            ConfigureOverlay(overlayHandle);

            // Run at display frequency in edit mode, one-third otherwise
            // TODO: react to refresh rate changes
            double updateInterval = 1.0 / (double)OpenVR.System.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float, ref error); // Device 0 should always be headset
            double qupdateInterval = updateInterval * 3;

            Stopwatch stopWatchFrame = new Stopwatch();
            stopWatchFrame.Start();
            Stopwatch stopWatchDoublePress = new Stopwatch();
            stopWatchDoublePress.Start();
            Stopwatch stopWatchEditing = new Stopwatch();

            Console.WriteLine("");
            Console.WriteLine("Pole overlay started! Press [R] to reload config. Press [E] to enter/exit edit mode");
            Console.WriteLine("In edit mode, double tap controller Trigger to snap pole to controller,");
            Console.WriteLine("press and hold trigger to move the overlay.");

            // Main Program Loop
            bool running = true;
            bool editMode = false;
            bool lastDoublePressed = false;
            bool dragging = false;
            uint draggingDevice = 0;
            Vector3 draggingStart = Vector3.Zero;
            Vector3 draggingCurrent = Vector3.Zero;
            TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            var settingserror = new EVRSettingsError();

            while (running)
            {
                // Main timing loop
                double deltaTime = stopWatchFrame.Elapsed.TotalMilliseconds / 1000;
                if ((deltaTime > qupdateInterval) || (editMode && (deltaTime > updateInterval)))
                {
                    stopWatchFrame.Restart();

                    // User input
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.Escape:
                                running = false;
                                break;
                            case ConsoleKey.R:
                                Console.WriteLine("Reloading config");
                                LoadSettings();
                                ConfigureOverlay(overlayHandle);
                                break;
                            case ConsoleKey.E:
                                editMode = !editMode;
                                if (editMode)
                                {
                                    Console.WriteLine("Edit mode ACTIVATED");
                                    stopWatchEditing.Restart();
                                }
                                else
                                {
                                    Console.WriteLine("Edit mode deactivated. Saving config");
                                    stopWatchEditing.Stop();
                                    SaveSettings();
                                    ConfigureOverlay(overlayHandle);
                                }
                                break;
                            default:
                                break;
                        }
                    }

                    // VR events
                    VREvent_t vrEvent = new VREvent_t();
                    while (OpenVR.System.PollNextEvent(ref vrEvent, uncbVREvent))
                    {
                        switch (vrEvent.eventType)
                        {
                            case (uint)EVREventType.VREvent_ImageLoaded: // Doesn't fire for some reason...
                                {
                                    Console.WriteLine("Event: Image laoded");
                                    //uint tw = 512, th = 1024;
                                    //OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.GetOverlayTextureSize(overlayHandle, ref tw, ref th));
                                    //aspect = (float)tw / th;
                                    //polematrix_scale = Matrix4x4.CreateScale(overlaywidth, Config.POLE_HEIGHT * aspect, overlaywidth);
                                }
                                break;
                            case (uint)EVREventType.VREvent_Quit:
                                Console.WriteLine("Event: Quitting...");
                                running = false;
                                break;
                            case (uint)EVREventType.VREvent_ReloadOverlays:
                                Console.WriteLine("Event: ReloadOverlays...");
                                LoadSettings();
                                ConfigureOverlay(overlayHandle);
                                break;

                                // Position adjustment by dragging
                            case (uint)EVREventType.VREvent_ButtonPress:
                                //Console.WriteLine("Event: ButtonPress");
                                if (!editMode || OpenVR.Overlay.IsDashboardVisible())
                                    break;
                                if (vrEvent.data.controller.button == (uint)EVRButtonId.k_EButton_SteamVR_Trigger)
                                {
                                    //Console.WriteLine("Event: Trigger down");
                                    long timedif = stopWatchDoublePress.ElapsedMilliseconds;
                                    stopWatchDoublePress.Restart();
                                    if ((timedif < 400) && (timedif > 100) && !lastDoublePressed)
                                    {
                                        //Console.WriteLine("Event: DOUBLE PRESS");
                                        lastDoublePressed = true;
                                        var targetdevice = vrEvent.trackedDeviceIndex;
                                        if (targetdevice >= OpenVR.k_unMaxTrackedDeviceCount)
                                        {
                                            Console.WriteLine("Cannot snap to controller - device id invalid");
                                            break;
                                        }
                                        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, 0, poses);
                                        if (!poses[targetdevice].bPoseIsValid)
                                        {
                                            Console.WriteLine("Cannot snap to controller - pose invalid");
                                            break;
                                        }
                                        var clickpos_raw = poses[targetdevice].mDeviceToAbsoluteTracking.ToMatrix4x4().Translation;
                                        config.POS_X = clickpos_raw.X;
                                        config.POS_Z = clickpos_raw.Z;

                                        float floorheight = 0;
                                        if (GetFloorHeight(ref floorheight))
                                        {
                                            // Unfortunately GetPlayAreaRect, GetWorkingStandingZeroPoseToRawTrackingPose are all affected by 'space drag' from OVRAS
                                            // so compensate by getting difference of tracking spaces/origins
                                            OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
                                            var clickpos_standing = poses[targetdevice].mDeviceToAbsoluteTracking.ToMatrix4x4().Translation;
                                            float y_calibrated = clickpos_standing.Y;
                                            config.POS_Y = floorheight - (clickpos_standing.Y - clickpos_raw.Y);
                                            //Console.WriteLine($"Compensated floor H: {config.POS_Y}");
                                        }
                                        //Console.WriteLine($"Snapping to: {config.POS_X}, {config.POS_Y}, {config.POS_Z}");

                                        ConfigureOverlay(overlayHandle);
                                    }
                                    else
                                    {
                                        lastDoublePressed = false;
                                        if (!dragging) // not already dragging with different controller
                                        {
                                            draggingDevice = vrEvent.trackedDeviceIndex;
                                            OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, 0, poses);
                                            if (!poses[draggingDevice].bPoseIsValid)
                                            {
                                                Console.WriteLine("Cannot start dragging - pose invalid");
                                                break;
                                            }
                                            dragging = true;
                                            draggingStart = poses[draggingDevice].mDeviceToAbsoluteTracking.ToMatrix4x4().Translation;
                                        }
                                    }
                                }
                                break;
                            case (uint)EVREventType.VREvent_ButtonUnpress:
                                if (!editMode)
                                {
                                    dragging = false;
                                    break;
                                }
                                if (vrEvent.data.controller.button == (uint)EVRButtonId.k_EButton_SteamVR_Trigger)
                                {
                                    if (dragging)
                                    {
                                        var dragresult = draggingStart - draggingCurrent;
                                        config.POS_X -= dragresult.X * config.DRAG_SCALE;
                                        config.POS_Z -= dragresult.Z * config.DRAG_SCALE;
                                        translation.X = config.POS_X;
                                        translation.Z = config.POS_Z;
                                        dragging = false;
                                    }
                                }
                                break;

                            case (uint)EVREventType.VREvent_ChaperoneUniverseHasChanged:
                            //    Console.WriteLine("Event: ChaperoneUniverseHasChanged");
                            //    {
                            //        Console.WriteLine("Event: ChaperoneDataHasChanged");
                            //        float newheight = 0;
                            //        GetFloorHeight(ref newheight);
                            //    }
                                break;
                            case (uint)EVREventType.VREvent_ChaperoneSettingsHaveChanged:
                                //Console.WriteLine("Event: ChaperoneSettingsHaveChanged");
                                // TODO: Hide overlay if chaperone and floor bounds are hidden (not forced and 0 fade distance)
                                // Unfortunately, OpenVR.Chaperone.ForceBoundsVisible is write-only...
                                //float fadedist = OpenVR.Settings.GetFloat(OpenVR.k_pch_CollisionBounds_Section, OpenVR.k_pch_CollisionBounds_FadeDistance_Float, ref settingserror);
                                //if (fadedist < 0.01)
                                break;
                        }
                    }
                    if (!running)
                        break;

                    // Main logic
                    Vector3 dragoffset = Vector3.Zero;
                    OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, 0, poses);
                    if (dragging)
                    {
                        if (poses[draggingDevice].bPoseIsValid)
                        {
                            draggingCurrent = poses[draggingDevice].mDeviceToAbsoluteTracking.ToMatrix4x4().Translation;
                            dragoffset = (draggingStart - draggingCurrent) * config.DRAG_SCALE;
                            dragoffset.Y = 0;
                        }
                    }
                    else
                    {
                        dragoffset = Vector3.Zero;
                    }
                    if (editMode)
                    {
                        // Pulse transparency to indicate edit mode is active
                        double blinktime = stopWatchEditing.ElapsedMilliseconds / 1000d * Math.PI;
                        float blinkalpha = 0.1f + (float)Math.Abs(Math.Cos(blinktime)) * 0.9f;
                        OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, config.TRANSPARENCY * blinkalpha));
                    }

                    // Rotate towards HMD to prevent culling
                    if (poses[0].bPoseIsValid)
                    {
                        //var hmdpos = poses[0].mDeviceToAbsoluteTracking.ToMatrix4x4().Translation - translation;
                        //hmdpos.Y = 0;
                        var hmdpos = new Vector3(poses[0].mDeviceToAbsoluteTracking.m3 - translation.X, 0, poses[0].mDeviceToAbsoluteTracking.m11 - translation.Z);
                        if (hmdpos.X == 0 && hmdpos.Z == 0)
                            continue;

                        // Distance fade
                        if (!editMode)
                        {
                            float distance = hmdpos.Length();
                            float fade = 1f - smootherstep(config.FADE_NEAR, config.FADE_FAR, distance);
                            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, config.TRANSPARENCY * fade));
                            if (fade < 0.001f) // invisible, can skip logic
                                continue;
                        }

                        float angle = MathF.Atan2(hmdpos.X, hmdpos.Z);
                        if (!float.IsNormal(angle))
                            continue;

                        var rotation = Matrix4x4.CreateRotationY(-angle);
                        var polematrix =  scale * rotation;

                        // Curved overlay is tangent to its coordinate, nudge to center it
                        var nudge = Vector3.Normalize(hmdpos) * (config.DIAMETER / 2f);

                        polematrix.Translation = translation - nudge - dragoffset;
                        var polematrix_vr = polematrix.ToHmdMatrix34_t();
                        OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseRawAndUncalibrated, ref polematrix_vr));
                    }
                }

                // Give up the rest of our time slice to anything else that needs to run
                // From MSDN: If the value of the millisecondsTimeout argument is zero, the thread relinquishes the remainder of its time slice to any thread of equal priority that is ready to run.
                // https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.sleep
                Thread.Sleep(0);
            }
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.DestroyOverlay(overlayHandle));
            OpenVR.Shutdown();
        }

        static bool GetFloorHeight(ref float height)
        {
            ChaperoneCalibrationState chapstate = OpenVR.Chaperone.GetCalibrationState();
            if (chapstate == ChaperoneCalibrationState.OK ||
                (chapstate >= ChaperoneCalibrationState.Warning) && (chapstate < ChaperoneCalibrationState.Warning_SeatedBoundsInvalid))
            {
                Console.WriteLine("Chaperone valid...");
                HmdQuad_t chaprect = new HmdQuad_t();
                if (OpenVR.Chaperone.GetPlayAreaRect(ref chaprect))
                {
                    float h = chaprect.vCorners0.v1;
                    height = h;
                    Console.WriteLine("Floor height = " + h);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("Chaperone invalid...");
            }
            return false;
        }

        static void LoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(settingsPath);
                    config = JsonSerializer.Deserialize<Configuration>(jsonString, serOptions) ?? new Configuration();
                    Console.WriteLine($"Using settings from {settingsPath}");

                    // Write config back, in case it's been updated
                    config.Validate();
                    string newConfigString = JsonSerializer.Serialize(config, serOptions);
                    File.WriteAllText(settingsPath, newConfigString);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception caught while reading {settingsPath}, using defaults");
                    Console.WriteLine(ex.ToString());
                }
            }
            else
            {
                Console.WriteLine($"No settings file found at {settingsPath}, using defaults");
                config = new Configuration();
                string jsonString = JsonSerializer.Serialize(config, serOptions);
                File.WriteAllText(settingsPath, jsonString);
            }
#if DEBUG
            Console.WriteLine("Configuration:");
            Console.WriteLine(JsonSerializer.Serialize(config, serOptions));
#endif
        }
        static void SaveSettings()
        {
            string jsonString = JsonSerializer.Serialize(config, serOptions);
            File.WriteAllText(settingsPath, jsonString);
        }

        static void ConfigureOverlay(ulong overlayHandle)
        {
            // Cache derived values
            float aspect = 0.5f; // TODO: retrieve from the texture
            float overlaywidth = config.DIAMETER * MathF.PI;
            scale = Matrix4x4.CreateScale(overlaywidth, config.HEIGHT * aspect, overlaywidth);
            translation.X = config.POS_X;
            translation.Y = config.POS_Y + config.HEIGHT / 2;
            translation.Z = config.POS_Z;

            string PoleTexPath = Path.Combine(new string[] { executablePath, config.FILENAME_IMG_POLE });

            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayFromFile(overlayHandle, PoleTexPath));
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayAlpha(overlayHandle, config.TRANSPARENCY));
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayColor(overlayHandle, config.COLOR_R / 255f, config.COLOR_G / 255f, config.COLOR_B / 255f));
            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlayCurvature(overlayHandle, 1f)); // easy cylinder :)

            if (config.ALWAYS_ON_TOP)
            {
                OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.SetOverlaySortOrder(overlayHandle, uint.MaxValue));
            }

            OVRUtilities.EVROverlayErrorHandler(OpenVR.Overlay.ShowOverlay(overlayHandle));
        }

        // https://en.wikipedia.org/wiki/Smoothstep
        static float smootherstep(float edge0, float edge1, float x)
        {
            // Scale, and clamp x to 0..1 range
            x = clamp((x - edge0) / (edge1 - edge0));
            return x * x * x * (x * (6.0f * x - 15.0f) + 10.0f);
        }
        static float clamp(float x, float lowerlimit = 0.0f, float upperlimit = 1.0f)
        {
            if (x < lowerlimit) return lowerlimit;
            if (x > upperlimit) return upperlimit;
            return x;
        }
    }
}