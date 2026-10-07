#!/usr/bin/env bash
set -euo pipefail

PACK_VERSION="5.4.2351"
PACK_URL="https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/${PACK_VERSION}/"
PLUGIN_GUID="com.sporadic.valheim.autojoin"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

fail() {
    printf 'Error: %s\n' "$*" >&2
    exit 1
}

find_game_dir() {
    local candidate

    if [[ -n "${VALHEIM_DIR:-}" ]]; then
        [[ -d "$VALHEIM_DIR" ]] || fail "VALHEIM_DIR does not exist: $VALHEIM_DIR"
        printf '%s\n' "$VALHEIM_DIR"
        return
    fi

    for candidate in \
        "$HOME/.local/share/Steam/steamapps/common/Valheim" \
        "$HOME/.steam/steam/steamapps/common/Valheim" \
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Valheim"; do
        if [[ -f "$candidate/valheim_Data/Managed/assembly_valheim.dll" ]]; then
            printf '%s\n' "$candidate"
            return
        fi
    done

    fail "Could not find Valheim. Set VALHEIM_DIR to the game install directory."
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "Required command '$1' is missing. Install it and rerun this script."
}

GAME_DIR="$(find_game_dir)"
[[ -f "$GAME_DIR/valheim_Data/Managed/assembly_valheim.dll" ]] || fail "Valheim managed assemblies are missing under $GAME_DIR"
[[ -f "$GAME_DIR/valheim_Data/Managed/assembly_utils.dll" ]] || fail "Valheim assembly_utils.dll is missing under $GAME_DIR"

require_command dotnet
require_command curl
require_command unzip

if [[ -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ]]; then
    printf 'BepInEx is already installed at %s; skipping pack installation.\n' "$GAME_DIR/BepInEx"
else
    printf 'Installing BepInExPack Valheim %s...\n' "$PACK_VERSION"
    TEMP_DIR="$(mktemp -d)"
    trap 'rm -rf "$TEMP_DIR"' EXIT
    curl --fail --location --retry 2 "$PACK_URL" --output "$TEMP_DIR/bepinex-pack.zip"
    unzip -q "$TEMP_DIR/bepinex-pack.zip" -d "$TEMP_DIR/unpacked"
    PACK_DIR="$TEMP_DIR/unpacked/BepInExPack_Valheim"
    [[ -f "$PACK_DIR/BepInEx/core/BepInEx.dll" ]] || fail "Downloaded pack did not contain BepInEx/core/BepInEx.dll"

    cp -a "$PACK_DIR/." "$GAME_DIR/"
    chmod u+x "$GAME_DIR/start_game_bepinex.sh"
    printf 'BepInEx pack installed.\n'
fi

PLUGIN_DIR="$GAME_DIR/BepInEx/plugins/ValheimAutoJoin"
mkdir -p "$PLUGIN_DIR"
export ValheimDir="$GAME_DIR"
dotnet build "$SCRIPT_DIR/ValheimAutoJoin.csproj" --configuration Release
cp "$SCRIPT_DIR/bin/Release/netstandard2.1/ValheimAutoJoin.dll" "$PLUGIN_DIR/ValheimAutoJoin.dll"
printf 'Plugin installed to %s\n' "$PLUGIN_DIR/ValheimAutoJoin.dll"

CONFIG_DIR="$GAME_DIR/BepInEx/config"
CONFIG_FILE="$CONFIG_DIR/${PLUGIN_GUID}.cfg"
mkdir -p "$CONFIG_DIR"

if [[ -f "$CONFIG_FILE" ]]; then
    printf 'Keeping existing plugin config: %s\n' "$CONFIG_FILE"
elif [[ -t 0 ]]; then
    printf '\nFirst-time AutoJoin settings\n'
    read -r -p 'Character/profile name (as shown in Valheim): ' character
    read -r -p 'Dedicated server address (host:port): ' server
    read -r -s -p 'Server password (leave blank if none): ' password
    printf '\n'
    [[ -n "$character" ]] || fail 'Character/profile name cannot be empty.'
    [[ -n "$server" ]] || fail 'Server address cannot be empty.'
    umask 077
    printf '[AutoJoin]\nCharacter = %s\nServer = %s\nPassword = %s\n' \
        "$character" "$server" "$password" > "$CONFIG_FILE"
    printf 'Created plugin config: %s\n' "$CONFIG_FILE"
else
    cat > "$CONFIG_FILE" <<'EOF'
[AutoJoin]
Character =
Server =
Password =
EOF
    printf 'Created blank plugin config; edit it before launching: %s\n' "$CONFIG_FILE"
fi

printf '\nInstallation complete. In Steam, set Valheim Launch Options to:\n'
printf '  ./start_game_bepinex.sh %%command%% -autojoin\n'
printf 'The Steam launch option keeps Steam in the launch path for overlay support.\n'