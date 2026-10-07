# Valheim AutoJoin Mod — AI Developer Implementation Guide

## Objective

Create a small BepInEx mod for Valheim that automatically:

1. Detects a `-autojoin` command-line argument.
2. Selects a predefined Valheim character.
3. Connects directly to a predefined dedicated server.
4. Supplies the predefined server password automatically.
5. Starts normal gameplay without requiring manual interaction with the Valheim main menu.

The target platform is Linux, specifically Steam Valheim running through BepInEx.

The preferred final interface is:

```text
./start_game_bepinex.sh %command% -autojoin
```

Configuration should live in:

```text
BepInEx/config/ValheimAutoJoin.cfg
```

Example:

```ini
[AutoJoin]

Character = Maks
Server = 192.168.1.222:2456
Password = MySecretPassword
```

Do **not** require the password to be supplied as a command-line argument in the final implementation because command-line arguments can be visible through process inspection.

---

# 1. Technology

Use:

* Valheim
* BepInEx
* C#
* .NET Standard 2.0
* Harmony
* Valheim's existing `FejdStartup` and `ZNet` methods

Do not use:

* GUI automation
* simulated mouse clicks
* AutoHotkey
* OCR
* modification of the Valheim executable
* unnecessary third-party Valheim frameworks such as Jötunn

The mod should call Valheim's existing methods directly.

---

# 2. BepInEx installation

The user's Valheim installation is expected at:

```text
~/.steam/steam/steamapps/common/Valheim/
```

After installing BepInExPack for Valheim, the installation should contain approximately:

```text
Valheim/
├── BepInEx/
├── start_game_bepinex.sh
├── doorstop_config.ini
├── winhttp.dll
├── valheim.x86_64
└── ...
```

On Linux:

```bash
cd ~/.steam/steam/steamapps/common/Valheim
chmod +x start_game_bepinex.sh
```

Steam launch options:

```text
./start_game_bepinex.sh %command%
```

---

# 3. Project setup

Create the development project outside the Valheim installation:

```bash
mkdir -p ~/src/ValheimAutoJoin
cd ~/src/ValheimAutoJoin
dotnet new classlib -n ValheimAutoJoin
cd ValheimAutoJoin
rm Class1.cs
```

The project should target:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
```

References are required to:

```text
BepInEx/core/BepInEx.dll
BepInEx/core/0Harmony.dll
valheim_Data/Managed/assembly_valheim.dll
valheim_Data/Managed/UnityEngine.dll
valheim_Data/Managed/UnityEngine.CoreModule.dll
```

A convenient environment variable is:

```bash
export ValheimDir="$HOME/.steam/steam/steamapps/common/Valheim"
```

---

# 4. Project file

Use a project file similar to:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>

    <AssemblyName>ValheimAutoJoin</AssemblyName>
    <RootNamespace>ValheimAutoJoin</RootNamespace>
  </PropertyGroup>

  <ItemGroup>

    <Reference Include="BepInEx">
      <HintPath>$(ValheimDir)/BepInEx/core/BepInEx.dll</HintPath>
    </Reference>

    <Reference Include="0Harmony">
      <HintPath>$(ValheimDir)/BepInEx/core/0Harmony.dll</HintPath>
    </Reference>

    <Reference Include="assembly_valheim">
      <HintPath>$(ValheimDir)/valheim_Data/Managed/assembly_valheim.dll</HintPath>
    </Reference>

    <Reference Include="UnityEngine">
      <HintPath>$(ValheimDir)/valheim_Data/Managed/UnityEngine.dll</HintPath>
    </Reference>

    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ValheimDir)/valheim_Data/Managed/UnityEngine.CoreModule.dll</HintPath>
    </Reference>

  </ItemGroup>

</Project>
```

Build with:

```bash
dotnet build -c Release
```

---

# 5. Plugin lifecycle

Create a BepInEx plugin:

```csharp
[BepInPlugin(
    "com.sporadic.valheim.autojoin",
    "Valheim Auto Join",
    "1.0.0")]
public class AutoJoinPlugin : BaseUnityPlugin
{
}
```

In `Awake()`:

1. Log that the plugin loaded.
2. Read `Environment.GetCommandLineArgs()`.
3. If `-autojoin` is absent, do nothing.
4. If present, load the configuration.
5. Start a coroutine waiting for Valheim's main menu/startup object.

Do not attempt to call `FejdStartup` immediately from `Awake()`, because Valheim's startup objects may not yet exist.

---

# 6. Configuration

Use BepInEx's configuration system rather than manually parsing a config file.

Example:

```csharp
private ConfigEntry<string> Character = null!;
private ConfigEntry<string> Server = null!;
private ConfigEntry<string> Password = null!;
```

Initialize:

```csharp
Character = Config.Bind(
    "AutoJoin",
    "Character",
    "",
    "Character/profile to automatically select.");

Server = Config.Bind(
    "AutoJoin",
    "Server",
    "",
    "Dedicated server address, e.g. 192.168.1.100:2456.");

Password = Config.Bind(
    "AutoJoin",
    "Password",
    "",
    "Dedicated server password.");
```

This creates:

```text
BepInEx/config/ValheimAutoJoin.cfg
```

with entries conceptually equivalent to:

```ini
[AutoJoin]

Character = Maks
Server = 192.168.1.222:2456
Password = MySecretPassword
```

---

# 7. Command-line switch

The plugin must only activate automatic joining when:

```text
-autojoin
```

is present.

Example:

```csharp
var args = Environment.GetCommandLineArgs();

if (!args.Contains("-autojoin"))
{
    Logger.LogInfo("AutoJoin disabled.");
    return;
}
```

The preferred final launch command is therefore:

```text
./start_game_bepinex.sh %command% -autojoin
```

Configuration should determine:

* character
* server
* password

rather than command-line arguments.

---

# 8. Wait for Valheim startup

Use a coroutine.

Conceptually:

```csharp
private IEnumerator WaitForMainMenu()
{
    while (FejdStartup.instance == null)
        yield return null;

    while (FejdStartup.instance.m_mainMenu == null)
        yield return null;

    yield return null;

    AutoJoin();
}
```

The extra frame is intentional. It allows the UI/startup state to finish initializing.

The implementation should also guard against multiple execution:

```csharp
private bool autoJoinStarted;
```

and:

```csharp
if (autoJoinStarted)
    yield break;

autoJoinStarted = true;
```

---

# 9. Character selection

Use Valheim's own character profile collection.

The relevant object is:

```csharp
FejdStartup.instance
```

and the relevant API is:

```csharp
FejdStartup.SelectCharacter(...)
```

Existing Valheim mods use `PlayerProfile.GetFilename()` and `FejdStartup.SelectCharacter()` for this workflow.

Find the configured character in:

```csharp
FejdStartup.instance.m_profiles
```

The implementation should support identifying the profile by its filename.

Example conceptual logic:

```csharp
PlayerProfile? profile = null;

foreach (var candidate in startup.m_profiles)
{
    if (string.Equals(
        candidate.GetFilename(),
        configuredCharacter,
        StringComparison.OrdinalIgnoreCase))
    {
        profile = candidate;
        break;
    }
}
```

If the current Valheim assembly exposes a reliable visible-name property such as `m_playerName`, optionally support that too.

If the character cannot be found:

1. Log an error.
2. List available character/profile filenames.
3. Abort auto-join.

Do not silently select the first character.

---

# 10. Server selection

Use Valheim's native dedicated-server join data.

The important type is:

```text
ServerJoinDataDedicated
```

Construct it from the configured address:

```text
192.168.1.222:2456
```

or:

```text
myserver.example.com:2456
```

Then pass the resulting server data to:

```csharp
FejdStartup.instance.SetServerToJoin(...)
```

Do not simulate typing the server into the GUI.

The existing Valheim startup API should handle the actual connection.

---

# 11. Initiate joining

After:

1. Selecting the character
2. Creating server join data
3. Calling `SetServerToJoin(...)`

call:

```csharp
FejdStartup.instance.JoinServer();
```

This should cause Valheim to proceed through its normal connection process.

---

# 12. Password handling

This is the part requiring the most care.

`JoinServer()` does not simply accept a password parameter.

The password is eventually involved in Valheim's client handshake.

The relevant method is:

```text
ZNet.RPC_ClientHandshake
```

Existing Valheim auto-connector mods patch the handshake and detect when:

```text
needPassword == true
```

The plugin should use Harmony to patch the appropriate handshake method.

Conceptually:

```csharp
[HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
private static class ClientHandshakePatch
{
    private static void Prefix(bool needPassword)
    {
        if (!needPassword)
            return;

        // Supply configured password through the
        // appropriate Valheim startup/password field.
    }
}
```

The exact method signature **must be verified against the user's installed `assembly_valheim.dll`**.

Do not blindly assume the signature because Valheim updates can change it.

Inspect the assembly using:

* ILSpy
* dnSpyEx
* dotPeek
* `monodis`
* another .NET decompiler

Search for:

```text
RPC_ClientHandshake
```

and:

```text
ServerPassword
```

---

# 13. Password implementation

Existing Valheim auto-connect implementations use the startup password field:

```csharp
FejdStartup.ServerPassword
```

when the handshake requests a password.

The intended flow is:

```text
JoinServer()
    ↓
Valheim connects
    ↓
server requests password
    ↓
ZNet.RPC_ClientHandshake(...)
    ↓
Harmony prefix sees needPassword == true
    ↓
set FejdStartup.ServerPassword
    ↓
normal Valheim handshake continues
```

The implementation should **not** bypass Valheim's authentication protocol.

It should merely supply the password to Valheim at the appropriate point.

---

# 14. Harmony initialization

If using Harmony manually:

```csharp
private Harmony harmony = null!;
```

In `Awake()`:

```csharp
harmony = new Harmony("com.sporadic.valheim.autojoin");
harmony.PatchAll();
```

Alternatively use BepInEx's standard Harmony integration.

Make sure the patch is installed before the server connection occurs.

---

# 15. Logging

Logging is important because startup timing and Valheim updates can make this feature difficult to debug.

Log at least:

```text
Valheim Auto Join loaded.
AutoJoin disabled.
AutoJoin enabled.
Waiting for FejdStartup.
FejdStartup found.
Looking for character 'Maks'.
Character selected.
Preparing server '192.168.1.222:2456'.
Joining server.
Server requested password.
Supplying automatic password.
```

Never log the actual password.

Bad:

```text
Supplying password: MySecretPassword
```

Good:

```text
Server requested password; supplying configured password.
```

---

# 16. Installation

After building:

```bash
dotnet build -c Release
```

copy:

```text
bin/Release/netstandard2.0/ValheimAutoJoin.dll
```

to:

```text
Valheim/BepInEx/plugins/ValheimAutoJoin/
```

For example:

```bash
mkdir -p "$ValheimDir/BepInEx/plugins/ValheimAutoJoin"

cp \
    bin/Release/netstandard2.0/ValheimAutoJoin.dll \
    "$ValheimDir/BepInEx/plugins/ValheimAutoJoin/"
```

---

# 17. Testing procedure

## Test 1 — plugin loads

Launch Valheim normally without `-autojoin`.

Inspect:

```text
BepInEx/LogOutput.log
```

Expected:

```text
Valheim Auto Join loaded.
AutoJoin disabled.
```

---

## Test 2 — character selection

Enable:

```text
-autojoin
```

but configure a valid server.

Verify that the correct character is selected.

If character selection fails, inspect:

```text
FejdStartup.m_profiles
```

and log all profile filenames.

---

## Test 3 — server connection

Configure:

```ini
Server = 192.168.1.222:2456
```

with no password initially if the server allows it.

Verify that:

```text
SetServerToJoin()
```

and:

```text
JoinServer()
```

successfully connect.

---

## Test 4 — password

Enable a password-protected server.

Verify the log contains:

```text
Server requested password; supplying configured password.
```

and that the game proceeds into the world without displaying the password UI.

---

# 18. Important security consideration

The password is stored in:

```text
BepInEx/config/ValheimAutoJoin.cfg
```

This is plaintext.

That is acceptable for a local convenience mod but should be documented.

Do not:

* log the password
* put it in source control
* put it in Steam launch arguments
* upload the config file publicly

Add the config file to `.gitignore` if the project is version controlled:

```gitignore
bin/
obj/
*.user
BepInEx/config/ValheimAutoJoin.cfg
```

---

# 19. Recommended final project

The finished project should look like:

```text
ValheimAutoJoin/
├── ValheimAutoJoin.csproj
├── AutoJoinPlugin.cs
├── .gitignore
└── README.md
```

The installed game should contain:

```text
Valheim/
└── BepInEx/
    ├── config/
    │   └── ValheimAutoJoin.cfg
    └── plugins/
        └── ValheimAutoJoin/
            └── ValheimAutoJoin.dll
```

---

# 20. Final user experience

Once installed, the user should only need to configure:

```ini
[AutoJoin]

Character = Maks
Server = 192.168.1.222:2456
Password = MySecretPassword
```

and put this in Steam launch options:

```text
./start_game_bepinex.sh %command% -autojoin
```

Then:

```text
Steam → Play Valheim
        ↓
BepInEx starts
        ↓
AutoJoin detects -autojoin
        ↓
Valheim main menu initializes
        ↓
configured character selected
        ↓
configured dedicated server selected
        ↓
JoinServer()
        ↓
server requests password
        ↓
Harmony supplies configured password
        ↓
Valheim enters the server
```

---

# 21. Development notes for an AI coding agent

Before writing the final code, inspect the **actual installed Valheim assembly**.

Do not invent APIs.

Specifically verify:

```text
FejdStartup
FejdStartup.instance
FejdStartup.m_profiles
FejdStartup.SelectCharacter(...)
FejdStartup.SetServerToJoin(...)
FejdStartup.JoinServer()
FejdStartup.ServerPassword
ServerJoinDataDedicated
ServerJoinData
ZNet.RPC_ClientHandshake(...)
PlayerProfile.GetFilename()
```

Valheim's internal APIs are not guaranteed to remain stable between versions.

If compilation fails, use the installed:

```text
valheim_Data/Managed/assembly_valheim.dll
```

as the authoritative API source.

The most likely version-specific areas are:

1. `PlayerProfile` member names.
2. `SelectCharacter()` parameters.
3. `ServerJoinDataDedicated` constructors.
4. `RPC_ClientHandshake()` parameters.
5. The exact mechanism by which `ServerPassword` is consumed.

The architecture should remain the same even if these signatures change.

---

# 22. Reference implementation pattern

Existing Valheim auto-connector mods demonstrate the relevant approach:

* use `FejdStartup.SelectCharacter()`
* use `FejdStartup.SetServerToJoin()`
* use `FejdStartup.JoinServer()`
* patch `ZNet.RPC_ClientHandshake()`
* provide the password when `needPassword` is true

A useful reference implementation is the source of the Valheim **AutoConnector** mod by MidnightMods. It can be consulted when adapting the implementation to the exact Valheim version being targeted.

The implementation should still verify all APIs against the locally installed Valheim assembly rather than copying version-specific code blindly.
