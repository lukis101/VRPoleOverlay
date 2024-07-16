using System;
using System.Drawing;
using System.IO;

namespace DJL.VRPoleOverlay
{
    internal class Configuration
    {
        // User-Configurable Settings

        public bool ALWAYS_ON_TOP = false;     // Whether to show over all other overlays or not
        public bool ASK_AUTOSTART = true;      // Whether to ask for registering app to auto-start with VR
        public bool USE_CHAPERONE_COLOR = true;  // Match color of SteamVR chaperone, custom COLOR_X params not overriden
        public bool USE_CHAPERONE_HEIGHT = true; // Match height of SteamVR chaperone, custom height param not overriden

        public byte COLOR_R = 255;             //
        public byte COLOR_G = 255;             // Tint color, 0-255
        public byte COLOR_B = 255;             //
        public float TRANSPARENCY = 0.3f;      // 0 - invisible, 1 - solid
        public float FADE_NEAR = 0.8f;         // Overlay is fully visible, when distance is lower than this
        public float FADE_FAR = 1.5f;          // Overlay is invisible, when distance is higher than this

        public float POS_X = 0.0f;             // Distance left/right of origin (negative is left)
        public float POS_Y = 0.0f;             // Distance up/down of origin (negative is down)
        public float POS_Z = 0.0f;             // Distance front/back of origin (negative is front)
        public float HEIGHT = 3.0f;            // Vertical height of the pole
        public float DIAMETER = 0.045f;        // Diameter of the pole

        public float DRAG_SCALE = 0.5f;        // Factor of actual position adjustment to drag distance, decreasing allows finer adjustment

        // All paths are relative to filepath of exe
        public string FILENAME_IMG_POLE = "Assets/texture.png"; // Custom texture (should only be png or jpg)

        // Non user-modifiable (Won't be serialized)

        internal readonly string SETTINGS_FILENAME = "settings.json"; // User-modifiable settings, generated
        internal readonly string MANIFEST_FILENAME = "vrpoleoverlay.vrmanifest"; // Used to set up SteamVR autostart

        internal readonly string APPLICATION_KEY = "lt.djl.vrpoleoverlay";
        internal readonly string OVERLAY_KEY = "lt.djl.vrpoleoverlay.pole";
        internal readonly string OVERLAY_NAME = "VRPoleOverlay";
        internal readonly string BINARY_PATH_WINDOWS = "VRPoleOverlay.exe";
        internal readonly string OVERLAY_DESCRIPTION = "OpenVR Overlay to show your fitness pole location";

        public void Validate()
        {
            float ClampFloat(float input, float min, float max) => MathF.Max(min, MathF.Min(input, max));
            byte ClampByte(byte input, byte max) => input > max ? max : input;

            //Configuration defaultConfig = new Configuration();

            const float coordinateLimitMin = -100;
            const float coordinateLimitMax =  100;
            POS_X = ClampFloat(POS_X, coordinateLimitMin, coordinateLimitMax);
            POS_Y = ClampFloat(POS_Y, coordinateLimitMin, coordinateLimitMax);
            POS_Z = ClampFloat(POS_Z, coordinateLimitMin, coordinateLimitMax);
            HEIGHT = ClampFloat(HEIGHT, 0.5f, 100f);
            DIAMETER = ClampFloat(DIAMETER, 0.01f, 1f);

            COLOR_R = ClampByte(COLOR_R, 255);
            COLOR_G = ClampByte(COLOR_R, 255);
            //COLOR_B = ClampByte(COLOR_R, 255);
            TRANSPARENCY = ClampFloat(TRANSPARENCY, 0f, 1f);
        }
    }
}