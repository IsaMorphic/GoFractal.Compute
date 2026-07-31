# GoFractal.Compute

Welcome to the home of the desktop rendering companion app for [GoFractal on Google Play](https://play.google.com/store/apps/details?id=com.ChosenFewSoftware.GoFractal)! GoFractal.Compute is a ready-made, open source command-line app that allows users to render their GoFractal formulas at high resolution with GPU acceleration. 

# How to Use

The following sections outline how users may render their fractal creations on a given desktop system. Importantly, the guide covers how to download, compile, and update their copy of GoFractal.Compute easily and efficiently. 

## Getting Started

First, ensure you have the following dependencies installed on your system:

1. [Git](https://git-scm.com/install/)
1. [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Next, run the following commands in a terminal:

```bash
# Fetch latest source code
git clone --recursive https://github.com/IsaMorphic/GoFractal.Compute.git

# Go to repository root directory
cd GoFractal.Compute
```

## Creating GoFractal Files (JSON format)

GoFractal configuration files are stored in JSON format and are directly compatible between the mobile app and GoFractal.Compute. To generate a configuration file that can be used between the two, open the GoFractal app on an Android mobile device, make something pretty, then click the "Save Configuration" button on the "Config" page. Save it to a cloud provider like DropBox or Google Drive so it may be accessed from a PC, then download them to a well-known directory. 

## Updating / Running the App

GoFractal.Compute comes pre-packaged with launcher scripts for all supported platforms to help users run the app easily and keep it up-to-date. The following commands should be run in a terminal application of the user's choice from within the repository root directory (chosen above). The scripts will fetch the latest source code for the app, compile it, and then run it all in one go, passing any command-line arguments to the program. 

### On Windows

```powershell
# Run application directly from source code (on Windows)
scripts\update-run.ps1
```

### On macOS

```bash
# Run application directly from source code (on macOS)
scripts/update-run.command
```

### On Linux

```bash
# Run application directly from source code (on Linux)
scripts/update-run.sh
```

# Credits

All FractalSharp and GoFractal code is handwritten by Isabelle Santin for Chosen Few Software. 