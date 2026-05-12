# 🪟 WindowsBar (Liquid Glass)

A sleek, Mac-like top menu bar for Windows built with **WPF** and **C# .NET 8**. It replaces the traditional taskbar feel with a modern, translucent "liquid glass" aesthetic that dynamically adapts to your desktop wallpaper.
Uses **~5–15MB RAM**
<p align="center">
  <img width="1919" height="1079" alt="image"
       src="https://github.com/user-attachments/assets/d5fec313-01d5-4b84-89e8-2c4fa6e51454" />
</p>

<p align="center">
  <img width="1916" height="1074" alt="image"
       src="https://github.com/user-attachments/assets/acd81e1a-7aac-4517-8daa-e6706f5676fc" />
</p>

## ✨ Features

* **Liquid Glass Aesthetic:** Uses native Windows Acrylic and dynamic color sampling  to seamlessly blend the bar's translucency with your current desktop wallpaper.
* **Centered Clock:** A perfectly anchored, central clock displaying the time, day, and date.
* **Active Window Tracking:** Displays the name of the currently focused application or window on the left side, right next to the custom Start button.
* **Dynamic Network Indicator:** Actively fetches your current Wi-Fi SSID and displays signal strength via dynamically animating Wi-Fi waves.
* **Battery Monitor:** Shows battery percentage and a charging indicator.
* **Notifications:** Clicking the Bell icon opens up the Notifications Panel.
* **Native Flyout Integration:** Seamlessly hooks into Windows 11's native flyouts. Clicking the Wi-Fi, Battery, or Volume icons opens the **Quick Settings / Action Center** just like the native taskbar.
* **System Tray Control:** Includes a hidden system tray icon to easily access settings, refresh colors, or cleanly exit the application.
* **Auto-Start Support:** Built-in settings menu to toggle "Launch at Windows startup" via the Registry.

## How to Use?
To run this your system should have .NET 8.0 Desktop Runtime . Install it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.26-windows-x64-installer) <br><br>
For people who only want to use the Software can Clone the project, keep only the "Windows Top Bar" folder and delete everything else.
Go to **Windows Top Bar\x64\Release\net8.0-windows** find the WindowsBar.exe file of 149KB, run it and Voila!  

  

<br>
<br>

**------------------------------------------------- For Developers who want to tweak --------------------------------------------------**
<br>
<br>

## 🛠️ Tech Stack

* **Framework:** C# / WPF / .NET 8.0
* **APIs Used:** * Windows Core Audio API (COM Interop for safe, native mute/volume status).
  * `user32.dll` (P/Invoke for keystroke simulation, window handles, and acrylic rendering).
  * `netsh` (For parsing raw Wi-Fi and signal strength data).

## 🚀 Getting Started

### Prerequisites
* Windows Windows 11 (build 22000+), tested on 24H2 26100+
* Visual Studio 2022
* .NET 8.0 Desktop Runtime

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/dipanjandeb/windows_bar_liquid_glass.git

## Build & Run

### Visual Studio 2022
1. Open `WindowsBar.sln`
2. Set platform to **x64** and Debug
3. Press **F5** to run in Debug mode, or select **Release** in place of Debug and **Ctrl+Shift+B** to build
4. Output: `WindowsBar\bin\x64\Release\net8.0-windows\WindowsBar.exe`

## Customization

Edit `MainBar.xaml` to tweak:
- `Height="36"` on the Window → change bar height
- `GradTop` / `GradBot` GradientStop colors → override tint
- `PillBorder` style → change pill opacity/radius
- `StatusText` style → change font size/color

---

## Troubleshooting

**Bar not staying on top after fullscreen games:**  
Some games grab exclusive fullscreen. Switch to Borderless Windowed in game settings.

**Colors don't match wallpaper:**  
Right-click tray → Refresh Colors.

**AppBar not reserving space (windows overlap bar):**  
Restart as Administrator once to let AppBar register. Subsequent runs work normally.

**Build error "System.Drawing.Common not found":**  
Ensure you're targeting `net8.0-windows` and run `dotnet restore`.
