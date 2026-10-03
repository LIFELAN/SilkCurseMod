using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using GlobalSettings;
using UnityEngine;

namespace SilkCurseMod;

/// <summary>
/// Per-save-file state of the curse, including the tool loadout that was backed up
/// when the curse was enabled.
/// </summary>
internal sealed class CurseProfileData
{
    public bool Active;
    public bool BackedUp;
    public string CrestId = string.Empty;
    public List<string> Tools = new();
    public Dictionary<string, string> Extras = new();

    /// <summary>Enemy kills accumulated since the curse was enabled (kill-to-uncurse mode only).</summary>
    public int Kills;

    /// <summary>True while the curse can only be lifted by killing enemies (F5 mode).</summary>
    public bool KillMode;
}

/// <summary>
/// Owns the global curse flag and handles applying/removing all of its effects at runtime.
/// </summary>
internal static class CurseController
{
    private const string Header =
        "# SilkCurseMod state file. Do not edit while the game is running.";
    // Format: profileId;active;backedUp;crestId;tool1|tool2|tool3;extraName=tool|extraName=tool;kills;killMode

    private static readonly Dictionary<int, CurseProfileData> Profiles = new();

    private static ToolCrest? _cursedCrest;
    private static int _profileId;
    private static bool _warnedNoToolManager;
    private static int _suppressFakeCursedEquippedDepth;
    private static bool _uncursePending;

    /// <summary>True while the fake curse is applied to the loaded save.</summary>
    internal static bool Active { get; private set; }

    /// <summary>
    /// While true, the patched <see cref="ToolCrest.IsEquipped"/> getter reports the vanilla
    /// value for the Cursed crest even though the fake curse is active. This is used while the
    /// game's attack recoil code (dash slash / down slash) reads the crest state, so that a
    /// Hunter-crest player keeps the Hunter recoil instead of inheriting the Witch/Cursed recoil.
    /// </summary>
    internal static bool SuppressFakeCursedEquipped => _suppressFakeCursedEquippedDepth > 0;

    internal static void PushSuppressFakeCursedEquipped()
    {
        _suppressFakeCursedEquippedDepth++;
    }

    internal static void PopSuppressFakeCursedEquipped()
    {
        if (_suppressFakeCursedEquippedDepth > 0)
        {
            _suppressFakeCursedEquippedDepth--;
        }
    }

    private static string StatePath => Path.Combine(Paths.ConfigPath, "SilkCurseMod.state.txt");

    // ------------------------------------------------------------------ persistence

    internal static void Load()
    {
        Profiles.Clear();
        try
        {
            if (!File.Exists(StatePath))
            {
                return;
            }

            foreach (var raw in File.ReadAllLines(StatePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = line.Split(';');
                if (parts.Length < 5 || !int.TryParse(parts[0], out var id))
                {
                    continue;
                }

                var data = new CurseProfileData
                {
                    Active = parts[1] == "1",
                    BackedUp = parts[2] == "1",
                    CrestId = parts[3],
                    Tools = parts[4].Length == 0 ? new List<string>() : parts[4].Split('|').ToList(),
                };

                if (parts.Length >= 6 && parts[5].Length > 0)
                {
                    foreach (var pair in parts[5].Split('|'))
                    {
                        var separator = pair.IndexOf('=');
                        if (separator > 0)
                        {
                            data.Extras[pair.Substring(0, separator)] = pair.Substring(separator + 1);
                        }
                    }
                }

                if (parts.Length >= 7 && int.TryParse(parts[6], out var kills))
                {
                    data.Kills = kills;
                }

                if (parts.Length >= 8)
                {
                    data.KillMode = parts[7] == "1";
                }

                Profiles[id] = data;
            }
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Failed to load SilkCurseMod state: " + e);
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.ConfigPath);
            var lines = new List<string> { Header };
            foreach (var (id, data) in Profiles)
            {
                lines.Add(string.Join(
                    ";",
                    id.ToString(),
                    data.Active ? "1" : "0",
                    data.BackedUp ? "1" : "0",
                    data.CrestId,
                    string.Join("|", data.Tools),
                    string.Join("|", data.Extras.Select(e => e.Key + "=" + e.Value)),
                    data.Kills.ToString(),
                    data.KillMode ? "1" : "0"));
            }

            File.WriteAllLines(StatePath, lines);
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Failed to save SilkCurseMod state: " + e);
        }
    }

    private static CurseProfileData GetProfile(int id)
    {
        if (!Profiles.TryGetValue(id, out var data))
        {
            data = new CurseProfileData();
            Profiles[id] = data;
        }

        return data;
    }

    // ------------------------------------------------------------------ profile sync

    internal static void SyncProfile(int profileId)
    {
        _profileId = profileId;
        Active = Profiles.TryGetValue(profileId, out var data) && data.Active;
        SilkCurseModPlugin.Log($"Synced save profile {profileId}, curse active = {Active}.");
    }

    private static int CurrentProfileId()
    {
        try
        {
            if (PlayerData.HasInstance)
            {
                return PlayerData.instance.profileID;
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            if (GameManager.UnsafeInstance != null)
            {
                return GameManager.UnsafeInstance.profileID;
            }
        }
        catch
        {
            // ignored
        }

        return 0;
    }

    // ------------------------------------------------------------------ toggling

    internal static void ToggleFromHotkey()
    {
        if (!PlayerData.HasInstance || HeroController.instance == null)
        {
            SilkCurseModPlugin.Log("Cannot toggle curse: no save file is currently loaded.");
            return;
        }

        _profileId = CurrentProfileId();
        var profile = GetProfile(_profileId);

        if (Active)
        {
            Disable(profile);
        }
        else
        {
            Enable(profile, killMode: false);
        }

        Save();
    }

    /// <summary>
    /// F5: enable the curse in "kill to uncurse" mode, or switch to that mode and reset the kill
    /// progress if the curse is already active.
    /// </summary>
    internal static void EnterKillModeFromHotkey()
    {
        if (!PlayerData.HasInstance || HeroController.instance == null)
        {
            SilkCurseModPlugin.Log("Cannot enter kill mode: no save file is currently loaded.");
            return;
        }

        _profileId = CurrentProfileId();
        var profile = GetProfile(_profileId);

        if (Active)
        {
            profile.KillMode = true;
            profile.Kills = 0;
        }
        else
        {
            Enable(profile, killMode: true);
        }

        Save();
        UncurseProgressPrompt.ShowProgress(0, Mathf.Max(1, SilkCurseModPlugin.KillsToUncurse.Value));
    }

    private static void Enable(CurseProfileData profile, bool killMode)
    {
        var pd = PlayerData.instance;

        profile.Active = true;
        Active = true;
        profile.KillMode = killMode;
        profile.Kills = 0;

        BackupTools(pd, profile);
        UnequipTools(pd);
        ApplyRuntime();

        SilkCurseModPlugin.Log($"Curse enabled (kill mode: {killMode}).");
    }

    private static void Disable(CurseProfileData profile)
    {
        profile.Active = false;
        Active = false;
        profile.KillMode = false;
        profile.Kills = 0;

        RestoreTools(profile);
        ApplyRuntime();

        SilkCurseModPlugin.Log("Curse disabled.");
    }

    // ------------------------------------------------------------------ kill to uncurse

    /// <summary>
    /// Called for every journal-recorded enemy kill (i.e. an enemy the hero damaged). Does nothing
    /// unless the curse is active and the kill-to-uncurse feature is enabled.
    /// </summary>
    internal static void RegisterEnemyKill()
    {
        try
        {
            if (!Active)
            {
                return;
            }

            var profile = GetProfile(_profileId);
            if (!profile.KillMode)
            {
                return;
            }

            var required = Mathf.Max(1, SilkCurseModPlugin.KillsToUncurse.Value);
            profile.Kills++;

            if (profile.Kills >= required)
            {
                // Defer the actual uncurse to the next Update so we are not mutating player/tool
                // state in the middle of the game's enemy-death callstack.
                profile.Kills = required;
                _uncursePending = true;
                UncurseProgressPrompt.ShowUncursed();
                return;
            }

            Save();
            UncurseProgressPrompt.ShowProgress(profile.Kills, required);
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("RegisterEnemyKill failed: " + e);
        }
    }

    /// <summary>Processes a pending kill-based uncurse. Called every frame by the plugin.</summary>
    internal static void Tick()
    {
        if (!_uncursePending)
        {
            return;
        }

        _uncursePending = false;

        try
        {
            var profile = GetProfile(_profileId);
            Disable(profile);
            Save();
            SilkCurseModPlugin.Log("Curse lifted by kills.");
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Kill uncurse failed: " + e);
        }
    }

    // ------------------------------------------------------------------ tool loadout

    private static void BackupTools(PlayerData pd, CurseProfileData profile)
    {
        if (profile.BackedUp)
        {
            return;
        }

        profile.BackedUp = true;
        profile.CrestId = pd.CurrentCrestID;

        profile.Tools.Clear();
        var data = pd.ToolEquips.GetData(profile.CrestId);
        if (data.Slots != null)
        {
            profile.Tools.AddRange(data.Slots.Select(slot => slot.EquippedTool ?? string.Empty));
        }

        profile.Extras.Clear();
        foreach (var name in pd.ExtraToolEquips.GetValidNames())
        {
            profile.Extras[name] = pd.ExtraToolEquips.GetData(name).EquippedTool ?? string.Empty;
        }

        SilkCurseModPlugin.Log(
            $"Backed up {profile.Tools.Count} slots for crest '{profile.CrestId}' and {profile.Extras.Count} extra slots.");
    }

    private static void UnequipTools(PlayerData pd)
    {
        if (!SilkCurseModPlugin.DisableTools.Value)
        {
            return;
        }

        if (ManagerSingleton<ToolItemManager>.Instance == null)
        {
            if (!_warnedNoToolManager)
            {
                _warnedNoToolManager = true;
                SilkCurseModPlugin.Log("ToolItemManager not ready yet, skipping tool unequip.");
            }

            return;
        }

        try
        {
            var crestId = pd.CurrentCrestID;
            if (string.IsNullOrEmpty(crestId))
            {
                return;
            }

            var data = pd.ToolEquips.GetData(crestId);
            var count = data.Slots?.Count ?? 0;

            var crest = ToolItemManager.GetCrestByName(crestId);
            if (crest?.Slots != null)
            {
                count = Mathf.Max(count, crest.Slots.Length);
            }

            if (count <= 0)
            {
                count = 3;
            }

            var empty = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                empty.Add(string.Empty);
            }

            ToolItemManager.SetEquippedTools(crestId, empty);

            foreach (var name in pd.ExtraToolEquips.GetValidNames())
            {
                ToolItemManager.SetExtraEquippedTool(name, string.Empty);
            }

            SilkCurseModPlugin.Log($"Unequipped {count} tool slots on crest '{crestId}'.");
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Failed to unequip tools: " + e);
        }
    }

    private static void RestoreTools(CurseProfileData profile)
    {
        if (!profile.BackedUp)
        {
            return;
        }

        if (ManagerSingleton<ToolItemManager>.Instance == null)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(profile.CrestId) && profile.Tools.Count > 0)
            {
                ToolItemManager.SetEquippedTools(profile.CrestId, new List<string>(profile.Tools));
            }

            foreach (var (slot, tool) in profile.Extras)
            {
                ToolItemManager.SetExtraEquippedTool(slot, tool);
            }

            profile.BackedUp = false;
            profile.Tools.Clear();
            profile.Extras.Clear();

            SilkCurseModPlugin.Log("Restored the previous tool loadout.");
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Failed to restore tools: " + e);
        }
    }

    // ------------------------------------------------------------------ runtime effects

    private static void ApplyRuntime()
    {
        try
        {
            var hero = HeroController.instance;
            if (hero != null)
            {
                if (Active)
                {
                    hero.UpdateSilkCursed();
                }

                // Clamp the current silk down to the (possibly lowered) cap.
                hero.RefreshSilk();
            }

            ToolItemManager.SendEquippedChangedEvent(force: true);

            var spool = GameCameras.SilentInstance != null ? GameCameras.SilentInstance.silkSpool : null;
            if (spool != null)
            {
                spool.DrawSpool();
            }
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Failed to apply curse runtime state: " + e);
        }
    }

    /// <summary>Called after the hero is (re)spawned, e.g. on scene load or respawn.</summary>
    internal static void OnHeroReady()
    {
        try
        {
            SyncProfile(CurrentProfileId());

            if (Active && PlayerData.HasInstance)
            {
                var pd = PlayerData.instance;
                BackupTools(pd, GetProfile(_profileId));
                UnequipTools(pd);
            }

            ApplyRuntime();
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("OnHeroReady failed: " + e);
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Only the currently loaded save's <see cref="PlayerData"/> should be faked as cursed.
    /// The save selection screen deserializes each save into a fresh PlayerData to build
    /// <see cref="SaveStats"/>; forcing the cursed getters on those would make every slot
    /// display a 3-slot spool.
    /// </summary>
    internal static bool IsLivePlayerData(PlayerData playerData)
    {
        return playerData != null
            && PlayerData.HasInstance
            && ReferenceEquals(playerData, PlayerData.instance);
    }

    internal static bool IsCursedCrest(ToolCrest crest)
    {
        if (crest == null)
        {
            return false;
        }

        if (_cursedCrest != null)
        {
            return ReferenceEquals(crest, _cursedCrest);
        }

        try
        {
            _cursedCrest = Gameplay.CursedCrest;
        }
        catch
        {
            // Gameplay settings not loaded yet.
        }

        if (_cursedCrest != null)
        {
            return ReferenceEquals(crest, _cursedCrest);
        }

        return crest.name == "Cursed";
    }
}
