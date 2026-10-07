# Valheim AutoJoin Mod

A BepInEx plugin for Linux Valheim that selects a configured local or cloud character and joins a dedicated server when launched with `-autojoin`.

## Requirements

- Valheim installed through Steam.
- .NET SDK available as `dotnet` (the project targets `netstandard2.1`).
- `curl` and `unzip` for the automated BepInEx pack installation.
- Bash to run the installer.

On CachyOS or Arch Linux, install the tools with:

```bash
sudo pacman -Syu --needed dotnet-sdk curl unzip
```

On Debian or Ubuntu, install `curl` and `unzip` with the system package manager and install a supported .NET SDK using Microsoft's .NET installation instructions. Mono is not needed to build or run this plugin.

## Automated installation

From this repository, run:

```bash
chmod +x install.sh
./install.sh
```

The installer:

1. Finds Valheim in the usual Steam library locations. To use another library, set `VALHEIM_DIR` to the Valheim game directory.
2. Checks for `BepInEx/core/BepInEx.dll`. If it exists, BepInEx pack installation is skipped. If it is absent, the installer downloads the pinned official Valheim BepInEx pack version `5.4.2351` from Thunderstore and installs it.
3. Builds the plugin against the assemblies in that Valheim installation and copies the DLL to `BepInEx/plugins/ValheimAutoJoin/`.
4. Creates the plugin config only if it does not already exist. On an interactive terminal it asks for the character, server address, and password; existing settings are never overwritten.

Example for a non-default Steam library:

```bash
VALHEIM_DIR="$HOME/Games/SteamLibrary/steamapps/common/Valheim" ./install.sh
```

If the installer reports that it created a blank config (for example, when run without an interactive terminal), edit:

```text
<Valheim directory>/BepInEx/config/com.sporadic.valheim.autojoin.cfg
```

Use this format:

```ini
[AutoJoin]
Character = YourCharacterProfile
Server = example.org:2456
Password = your-server-password
```

The character value must be the character's profile filename/name as shown in the plugin's available-profile log; cloud profiles are supported. Leave `Password` empty if the server has no password.

## Manual installation

Use these steps if you prefer not to run `install.sh`.

1. Set the game directory. Adjust it if Valheim is in another Steam library:

   ```bash
   export ValheimDir="$HOME/.local/share/Steam/steamapps/common/Valheim"
   ```

   The common `~/.steam/steam/steamapps/common/Valheim` path is often a symlink to the same directory. Confirm the selected directory contains `valheim_Data/Managed/assembly_valheim.dll`.

2. Install BepInExPack Valheim from the official [Thunderstore package page](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). For a command-line install of the version used by the automated installer:

   ```bash
   cd "$ValheimDir"
   curl -fL 'https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/5.4.2351/' -o /tmp/BepInExPack_Valheim.zip
   unzip -o /tmp/BepInExPack_Valheim.zip -d /tmp
   cp -a /tmp/BepInExPack_Valheim/. "$ValheimDir/"
   chmod u+x "$ValheimDir/start_game_bepinex.sh"
   ```

   The pack is installed when `BepInEx/core/BepInEx.dll`, `doorstop_config.ini`, and `start_game_bepinex.sh` are present in the game directory. If BepInEx is already installed, do not repeat the pack extraction over a working setup just to install this plugin.

3. Build and copy the plugin from the repository directory:

   ```bash
   export ValheimDir="$HOME/.local/share/Steam/steamapps/common/Valheim"
   dotnet build -c Release
   mkdir -p "$ValheimDir/BepInEx/plugins/ValheimAutoJoin"
   cp bin/Release/netstandard2.1/ValheimAutoJoin.dll \
      "$ValheimDir/BepInEx/plugins/ValheimAutoJoin/ValheimAutoJoin.dll"
   ```

4. Create the config directory and file if they do not exist, then edit the file with your values:

   ```bash
   mkdir -p "$ValheimDir/BepInEx/config"
   touch "$ValheimDir/BepInEx/config/com.sporadic.valheim.autojoin.cfg"
   nano "$ValheimDir/BepInEx/config/com.sporadic.valheim.autojoin.cfg"
   ```

   Enter the `[AutoJoin]` example shown above. The plugin also creates its config on first launch, but it needs valid `Character` and `Server` values to join.

## Launch through Steam

To keep Steam in the launch path and retain Steam Overlay support:

1. Open **Valheim → Properties → General → Launch Options** in Steam.
2. Set the launch option to:

   ```text
   ./start_game_bepinex.sh %command% -autojoin
   ```

3. Start Valheim from Steam.

Do not put `-autojoin` before `%command%`; the BepInEx launcher must receive Steam's game command first. For a direct terminal test, run from the game directory:

```bash
cd "$HOME/.local/share/Steam/steamapps/common/Valheim"
./start_game_bepinex.sh ./valheim.x86_64 -autojoin
```

Launching the game binary directly is useful for testing but may not provide Steam Overlay. Use the Steam Launch Options method for normal play.

## Logs and troubleshooting

- BepInEx log: `<Valheim directory>/BepInEx/LogOutput.log`.
- The plugin reports whether `-autojoin` was detected, whether config fields are set, the matched character's storage source, main-menu readiness, and join/password progress. It does not log the configured password.
- If the character is not found, check the logged available profile names and verify the configured character value. Local and cloud save sources are both supported.
- If the server asks for a password again, check that the active config is `BepInEx/config/com.sporadic.valheim.autojoin.cfg` and that its `Password` field is set correctly.
- The Steam launch option must be exactly `./start_game_bepinex.sh %command% -autojoin` for the documented setup.

## Security

The server password is stored as plain text in the local BepInEx config. The automated installer asks for it without echoing it to the terminal and creates the file with owner-only permissions, but the password remains readable by your user and by software running as that user. Do not commit the config or place the password in Steam launch options.