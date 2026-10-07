using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace ValheimAutoJoin
{
    [BepInPlugin("com.sporadic.valheim.autojoin", "Valheim Auto Join", "1.0.0")]
    public class AutoJoinPlugin : BaseUnityPlugin
    {
        private ConfigEntry<string> Character = null!;
        private ConfigEntry<string> Server = null!;
        private ConfigEntry<string> Password = null!;

        private Harmony? harmony;
        private bool autoJoinStarted;

        internal static AutoJoinPlugin? Instance { get; private set; }
        internal static string ConfiguredPassword { get; private set; } = string.Empty;

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("Valheim Auto Join loaded.");

            var args = Environment.GetCommandLineArgs();
            if (!args.Any(arg => string.Equals(arg, "-autojoin", StringComparison.OrdinalIgnoreCase)))
            {
                Logger.LogInfo("AutoJoin disabled.");
                return;
            }

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

            ConfiguredPassword = Password.Value;

            Logger.LogInfo("AutoJoin enabled.");
            Logger.LogInfo($"AutoJoin settings: character {(string.IsNullOrWhiteSpace(Character.Value) ? "missing" : "configured")}, server {(string.IsNullOrWhiteSpace(Server.Value) ? "missing" : "configured")}, password {(string.IsNullOrEmpty(Password.Value) ? "missing" : "configured")}.");

            harmony = new Harmony("com.sporadic.valheim.autojoin");
            harmony.PatchAll();

            StartCoroutine(WaitForMainMenu());
        }

        private IEnumerator WaitForMainMenu()
        {
            if (autoJoinStarted)
                yield break;

            autoJoinStarted = true;

            if (FejdStartup.instance == null)
                Logger.LogInfo("Waiting for FejdStartup.");

            while (FejdStartup.instance == null)
            {
                yield return null;
            }

            Logger.LogInfo("FejdStartup is ready; waiting for the main menu.");

            while (!HasMainMenuReady())
            {
                yield return null;
            }

            Logger.LogInfo("Main menu is ready; starting auto-join.");
            yield return null;
            AutoJoin();
        }

        private bool HasMainMenuReady()
        {
            var startup = FejdStartup.instance;
            if (startup == null)
                return false;

            var field = typeof(FejdStartup).GetField("m_mainMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return field != null && field.GetValue(startup) != null;
        }

        private void AutoJoin()
        {
            var startup = FejdStartup.instance;
            if (startup == null)
            {
                Logger.LogError("FejdStartup instance is null; aborting auto-join.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Character.Value))
            {
                Logger.LogError("No character configured; auto-join aborted.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Server.Value))
            {
                Logger.LogError("No server configured; auto-join aborted.");
                return;
            }

            var profile = FindProfile(startup, Character.Value);
            if (profile == null)
            {
                var availableProfiles = GetProfiles(startup)
                    .Select(p => $"{p.GetFilename()} ({p.m_fileSource})")
                    .Where(profileName => !string.IsNullOrEmpty(profileName))
                    .ToArray();

                Logger.LogError($"Character '{Character.Value}' was not found. Available profiles: {string.Join(", ", availableProfiles)}");
                return;
            }

            Logger.LogInfo($"Selecting character '{profile.GetFilename()}' from {profile.m_fileSource} storage.");
            InvokeSelectCharacter(startup, profile);
            Logger.LogInfo("Character selected.");

            Logger.LogInfo($"Preparing server '{Server.Value}'.");
            var dedicated = new ServerJoinDataDedicated(Server.Value);
            var joinData = new ServerJoinData(dedicated);
            startup.SetServerToJoin(joinData);

            if (!SetConfiguredPassword())
            {
                Logger.LogError("Could not set Valheim's server password; aborting join.");
                return;
            }

            Logger.LogInfo("Configured password set before starting the server join.");

            Logger.LogInfo("Joining server.");
            startup.JoinServer();
        }

        private static PlayerProfile? FindProfile(FejdStartup startup, string configuredCharacter)
        {
            foreach (var profile in GetProfiles(startup))
            {
                if (profile == null)
                    continue;

                var filename = profile.GetFilename();
                if (string.Equals(filename, configuredCharacter, StringComparison.OrdinalIgnoreCase))
                    return profile;
            }

            return null;
        }

        private static List<PlayerProfile> GetProfiles(FejdStartup startup)
        {
            var field = typeof(FejdStartup).GetField("m_profiles", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                return new List<PlayerProfile>();

            var value = field.GetValue(startup);
            if (value is IEnumerable<PlayerProfile> typed)
                return typed.ToList();

            if (value is IEnumerable enumerable)
            {
                var list = new List<PlayerProfile>();
                foreach (var item in enumerable)
                {
                    if (item is PlayerProfile profile)
                        list.Add(profile);
                }

                return list;
            }

            return new List<PlayerProfile>();
        }

        private static void InvokeSelectCharacter(FejdStartup startup, PlayerProfile profile)
        {
            var method = typeof(FejdStartup).GetMethod("SelectCharacter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("FejdStartup.SelectCharacter(...) was not found in the installed Valheim assembly.");
            }

            method.Invoke(startup, new object[] { profile.GetFilename(), profile.m_fileSource });
        }

        private static bool SetConfiguredPassword()
        {
            var passwordField = typeof(FejdStartup).GetField("<ServerPassword>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            if (passwordField == null)
                return false;

            passwordField.SetValue(null, ConfiguredPassword);
            return true;
        }

        [HarmonyPatch(typeof(ZNet), "RPC_ClientHandshake")]
        private static class ClientHandshakePatch
        {
            private static void Prefix(ZRpc rpc, bool needPassword, string serverPasswordSalt)
            {
                if (!needPassword)
                    return;

                if (FejdStartup.instance == null)
                    return;

                if (string.IsNullOrEmpty(ConfiguredPassword))
                    return;

                if (SetConfiguredPassword())
                    Instance?.Logger.LogInfo("Server requested password; supplying configured password.");
            }
        }
    }
}
