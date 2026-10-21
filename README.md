# Telltale Mod Launcher

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/Telltale-Modding-Group/TelltaleModLauncher)](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/releases)
[![Github All Releases](https://img.shields.io/github/downloads/Telltale-Modding-Group/TelltaleModLauncher/total.svg)](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/releases)  

# [DOWNLOAD HERE](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/releases)!

**Releases are now being put out! You can get them [HERE](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/releases)!**
**If you need help or have questions about the application, click [HERE](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/wiki/%5BHelp%5D-Application-Setup)!**
**If your looking for Mods to get, supported mods can be found [HERE](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/wiki/Supported-Mods)!** **[Discord Server](https://discord.gg/rn8HvqZjZZ)**

## DISCLAIMER
***NOTE: This project is a work in progress. Some features have not been implemented just yet and there could be issues/bugs that you might encounter. If you do please follow [this](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/wiki/%5BHelp%5D---Reporting-an-Issue-or-Bug) to submit an issue.***

***NOTE (3/16/21): Creator functionality is being stripped and moved to the [Telltale Script Editor](https://github.com/Telltale-Modding-Group/Telltale-Script-Editor) as of the next release V0.7.0 which will be coming soon [(biggest update yet)](https://github.com/Telltale-Modding-Group/TelltaleModLauncher/blob/main/changes.txt).***

***NOTE (9/6/21): Apologies for the very very late update. I cannot predict when V0.7.0 will finally come but in the meantime I've updated the launcher to 0.6.5 which is mostly a quality-of-life update which makes the setup of the Launcher alot more simpler and less confusing (despite making a tutorial that is linked on this page that guides you how to set it up properly...)***

## Supported Games
The launcher has been tested with the following game.
- **The Walking Dead: The Telltale Definitive Series**

Any other Telltale game can be added from the game list. These are marked as untested, since the launcher has only been tried with the Definitive Series so far.

## Windows and Linux
The launcher runs on Windows and Linux.

On Linux, point it at the game's .exe inside your Steam library (for example `~/.local/share/Steam/steamapps/common/<game folder>`). Play starts the game through Steam so it runs under Proton. A copy that isn't in a Steam library runs with Wine if `wine` is installed.

## About
This launcher is designed to make installing and managing mods for Telltale Games much easier through a very user friendly interface.

**If you wish to make mods, we have the following tools avaliable.**
- **[Texture Mods](https://github.com/Telltale-Modding-Group/DDS-D3DTX-Converter)** **[(Tutorial)](https://github.com/Telltale-Modding-Group/DDS-D3DTX-Converter/wiki/%5BTutorial%5D--How-to-make-a-Texture-Mod-(Part-1))**
- **[Scripting Mods](https://github.com/Telltale-Modding-Group/Telltale-Script-Editor)**

Additional info can be found and asked in our **[Discord Server](https://discord.gg/rn8HvqZjZZ)**.

![Screenshot 1](screenshots/shot1.png)

## Building
You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet build
dotnet test
dotnet run --project src/TelltaleModLauncher
```

To make a single file build for one platform:

```
dotnet publish src/TelltaleModLauncher -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
dotnet publish src/TelltaleModLauncher -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

The code is split into `src/TelltaleModLauncher.Core` (settings, mod install and removal, Steam lookup) and `src/TelltaleModLauncher` (the Avalonia app). Tests are in `tests/TelltaleModLauncher.Tests`. Set `SCREENSHOT_DIR` when running the tests to save renders of each window.

The bundled Barlow fonts are under the SIL Open Font License, included next to them in `src/TelltaleModLauncher/Assets/Fonts/OFL.txt`.
