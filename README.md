<h1 align="center">
  <br>
  <img src="https://raw.githubusercontent.com/GreemDev/Ryujinx/master/distribution/misc/Logo.svg" alt="Ryujinx" width="150"></a>
  <br>
  <b>Ryujinx</b>
  <br>
  <sub><sup><b>(REE-YOU-JINX)</b></sup></sub>
  <br>
</h1>

<p align="center">
  Ryujinx is an open-source Nintendo Switch emulator, originally created by gdkchan, written in C#.
  This emulator aims at providing excellent accuracy and performance, a user-friendly interface and consistent builds.
  It was written from scratch and development on the project began in September 2017.
  Ryujinx is available on Github under the <a href="https://github.com/GreemDev/Ryujinx/blob/master/LICENSE.txt" target="_blank">MIT license</a>.
  <br />
</p>
<p align="center">
  On October 1st 2024, Ryujinx was discontinued as the creator was forced to abandon the project.
  This fork is intended to be a direct continuation for existing Ryujinx users.
  Guides and documentation will not be provided at this time, though you can find the old ones on the Internet Archive.
</p>
<p align="center">
  If you would like a version more true to original Ryujinx, check out <a href="https://github.com/ryujinx-mirror/ryujinx">ryujinx-mirror</a>.
</p>

<p align="center">
    <a href="https://github.com/GreemDev/Ryujinx/actions/workflows/release.yml">
        <img src="https://github.com/GreemDev/Ryujinx/actions/workflows/release.yml/badge.svg"
            alt="">
    </a>
    <a href="https://crwd.in/ryujinx">
        <img src="https://badges.crowdin.net/ryujinx/localized.svg"
            alt="">
    </a>
    <a href="https://discord.gg/dHPrkBkkyA">
        <img src="https://img.shields.io/discord/1294443224030511104?color=5865F2&label=Ryujinx&logo=discord&logoColor=white"
            alt="Discord">
    </a>
    <br>
    <br>
    <img src="https://raw.githubusercontent.com/GreemDev/Ryujinx/refs/heads/master/docs/shell.png">
</p>

## Compatibility

As of May 2024, Ryujinx has been tested on approximately 4,300 titles;
over 4,100 boot past menus and into gameplay, with roughly 3,550 of those being considered playable.

Anyone is free to submit a new game test or update an existing game test entry;
simply follow the new issue template and testing guidelines, or post as a reply to the applicable game issue.
Use the search function to see if a game has been tested already!

## Usage

To run this emulator, your PC must be equipped with at least 8GiB of RAM;
failing to meet this requirement may result in a poor gameplay experience or unexpected crashes.

## Latest build

These builds are compiled automatically for each commit on the master branch.
While we strive to ensure optimal stability and performance prior to pushing an update, our automated builds **may be unstable or completely broken**.

## Documentation

If you are planning to contribute or just want to learn more about this project please read through our [documentation](docs/README.md).

## Building

If you wish to build the emulator yourself, follow these steps:

### Step 1

Install the [.NET 8.0 (or higher) SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
Make sure your SDK version is higher or equal to the required version specified in [global.json](global.json). 

### Step 2

Either use `git clone https://github.com/GreemDev/Ryujinx` on the command line to clone the repository or use Code --> Download zip button to get the files.

### Step 3

To build Ryujinx, open a command prompt inside the project directory.
You can quickly access it on Windows by holding shift in File Explorer, then right clicking and selecting `Open command window here`.
Then type the following command: `dotnet build -c Release -o build`
the built files will be found in the newly created build directory.

Ryujinx system files are stored in the `Ryujinx` folder.
This folder is located in the user folder, which can be accessed by clicking `Open Ryujinx Folder` under the File menu in the GUI.

## Features

- **Audio**

  Audio output is entirely supported, audio input (microphone) isn't supported.
  We use C# wrappers for [OpenAL](https://openal-soft.org/), and [SDL2](https://www.libsdl.org/) & [libsoundio](http://libsound.io/) as fallbacks.

- **CPU**

  The CPU emulator, ARMeilleure, emulates an ARMv8 CPU and currently has support for most 64-bit ARMv8 and some of the ARMv7 (and older) instructions, including partial 32-bit support.
  It translates the ARM code to a custom IR, performs a few optimizations, and turns that into x86 code.
  There are three memory manager options available depending on the user's preference, leveraging both software-based (slower) and host-mapped modes (much faster).
  The fastest option (host, unchecked) is set by default.
  Ryujinx also features an optional Profiled Persistent Translation Cache, which essentially caches translated functions so that they do not need to be translated every time the game loads. 
  The net result is a significant reduction in load times (the amount of time between launching a game and arriving at the title screen) for nearly every game.
  NOTE: This feature is enabled by default in the Options menu > System tab.
  You must launch the game at least twice to the title screen or beyond before performance improvements are unlocked on the third launch!
  These improvements are permanent and do not require any extra launches going forward.

- **GPU**

  The GPU emulator emulates the Switch's Maxwell GPU using either the OpenGL (version 4.5 minimum), Vulkan, or Metal (via MoltenVK) APIs through a custom build of OpenTK or Silk.NET respectively.
  There are currently six graphics enhancements available to the end user in Ryujinx: Disk Shader Caching, Resolution Scaling, Anti-Aliasing, Scaling Filters (including FSR), Anisotropic Filtering and Aspect Ratio Adjustment.
  These enhancements can be adjusted or toggled as desired in the GUI.

- **Input**

  We currently have support for keyboard, mouse, touch input, JoyCon input support, and nearly all controllers.
  Motion controls are natively supported in most cases; for dual-JoyCon motion support, DS4Windows or BetterJoy are currently required.
  In all scenarios, you can set up everything inside the input configuration menu.

- **DLC & Modifications**

  Ryujinx is able to manage add-on content/downloadable content through the GUI.
  Mods (romfs, exefs, and runtime mods such as cheats) are also supported;
  the GUI contains a shortcut to open the respective mods folder for a particular game.

- **Configuration**

  The emulator has settings for enabling or disabling some logging, remapping controllers, and more.
  You can configure all of them through the graphical interface or manually through the config file, `Config.json`, found in the user folder which can be accessed by clicking `Open Ryujinx Folder` under the File menu in the GUI.

## License

This software is licensed under the terms of the [MIT license](LICENSE.txt).
This project makes use of code authored by the libvpx project, licensed under BSD and the ffmpeg project, licensed under LGPLv3.
See [LICENSE.txt](LICENSE.txt) and [THIRDPARTY.md](distribution/legal/THIRDPARTY.md) for more details.

## Credits

- [LibHac](https://github.com/Thealexbarney/LibHac) is used for our file-system.
- [AmiiboAPI](https://www.amiiboapi.com) is used in our Amiibo emulation.
- [ldn_mitm](https://github.com/spacemeowx2/ldn_mitm) is used for one of our available multiplayer modes.
- [ShellLink](https://github.com/securifybv/ShellLink) is used for Windows shortcut generation.
