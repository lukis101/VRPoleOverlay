# VRPoleOverlay

### [Download Here](https://github.com/rrazgriz/VRPoleOverlay/releases/latest)

Project based on [VRCMicOverlay by rrazgriz](https://github.com/rrazgriz/VRCMicOverlay) (MIT licensed)  
Edits and new additions are also licensed under MIT  

OpenVR Overlay written in C# that has basic cylinder shape to represent a stationary fitness pole in a playspace.  
Includes in-VR calibration/positioning using controllers, basic interactions via console, and a few customisations via a config file.  

## Usage

1. **Download** [the latest release](https://github.com/rrazgriz/VRPoleOverlay/releases/latest) and **extract** to your preferred folder. All files are necessary. 
2. **Run** `VRPoleOverlay.exe` with SteamVR open. The first time, you'll get asked if you want to make the app auto-start with SteamVR. Don't select yes if you plan on changing the program location after trying it out!
3. **Calibrate** the pole:  
  3.1. Hit `E` key in console to enter edit mode (indicated by the pole 'blinking').  
  3.2. Using VR controller, **double-press trigger** to bring the pole to it.  
  3.3. **Hold** the trigger to fine-adjust overlay position.  
  3.4. Hit `E` again to exit edit mode and save.
4. _Optional:_ Edit the generated `settings.json` to your liking. Hit `R` key in console to reload config without restarting. For details on what each setting does, see the comments in [Configuration.cs](VRPoleOverlay/Configuration.cs).
5. _Optional:_ Customize the texture inside `Assets` folder. Keep the aspect ratio same. Preferably keep texture consistent horizontally because the overlay gets rotated dynamically.

## Building

This should be buildable with a .NET 8 SDK. `cd` to the VRPoleOverlay subfolder, `dotnet restore`, then `dotnet build`.
