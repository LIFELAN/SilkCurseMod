using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SilkCurseMod;

[BepInAutoPlugin(id: "io.github.lifelan.silkcursemod")]
public partial class SilkCurseModPlugin : BaseUnityPlugin
{
    internal static SilkCurseModPlugin Instance { get; private set; } = null!;

    internal static ConfigEntry<KeyCode> Hotkey = null!;
    internal static ConfigEntry<KeyCode> KillModeHotkey = null!;
    internal static ConfigEntry<bool> DisableTools = null!;
    internal static ConfigEntry<int> KillsToUncurse = null!;
    internal static ConfigEntry<bool> DebugLogging = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Instance = this;

        Hotkey = Config.Bind(
            "General",
            "Hotkey",
            KeyCode.F6,
            new ConfigDescription(
                "Key that toggles the 畸芽诅咒 (curse) for the currently loaded save file. This mode must be closed manually with the same key. Click the button and press a key to rebind.",
                null,
                new ConfigurationManagerAttributes { CustomHotkeyDrawer = DrawHotkeyField }));

        KillModeHotkey = Config.Bind(
            "General",
            "KillModeHotkey",
            KeyCode.F5,
            new ConfigDescription(
                "Key that enables the curse in kill-to-uncurse mode (or resets its progress). In this mode the curse can only be lifted by killing enemies, and it shows a progress popup. Click the button and press a key to rebind.",
                null,
                new ConfigurationManagerAttributes { CustomHotkeyDrawer = DrawHotkeyField }));

        DisableTools = Config.Bind(
            "Curse",
            "DisableTools",
            true,
            "While cursed, all equipped tools and skills are unequipped. The previous loadout is restored when the curse is toggled off.");

        KillsToUncurse = Config.Bind(
            "Curse",
            "KillsToUncurse",
            10,
            new ConfigDescription(
                "Number of journal-recorded enemy kills required to lift the curse in kill mode.",
                new AcceptableValueRange<int>(1, 999)));

        DebugLogging = Config.Bind(
            "General",
            "DebugLogging",
            false,
            "Log extra debugging information to the BepInEx console/log.");

        _harmony = new Harmony(Info.Metadata.GUID);
        _harmony.PatchAll(typeof(CursePatches));

        CurseController.Load();

        Logger.LogInfo($"{Info.Metadata.Name} {Info.Metadata.Version} loaded. Hotkey: {Hotkey.Value}");
    }

    private void Update()
    {
        CurseController.Tick();

        var killModeKey = KillModeHotkey.Value;
        if (killModeKey != KeyCode.None && Input.GetKeyDown(killModeKey))
        {
            CurseController.EnterKillModeFromHotkey();
        }

        var toggleKey = Hotkey.Value;
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            CurseController.ToggleFromHotkey();
        }
    }

    private static void DrawHotkeyField(ConfigEntryBase setting, ref bool isEditing)
    {
        if (setting is not ConfigEntry<KeyCode> entry)
        {
            return;
        }

        if (isEditing)
        {
            // Stop the key press from leaking into the search box or another field.
            GUIUtility.keyboardControl = -1;

            GUILayout.Label("Press any key (Esc to cancel)", GUILayout.ExpandWidth(true));

            var current = Event.current;
            if (current != null && current.isKey && current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Escape)
                {
                    isEditing = false;
                }
                else if (!IsModifierKey(current.keyCode))
                {
                    entry.Value = current.keyCode;
                    isEditing = false;
                }

                current.Use();
            }
        }
        else if (GUILayout.Button(entry.Value.ToString(), GUILayout.ExpandWidth(true)))
        {
            isEditing = true;
        }
    }

    private static bool IsModifierKey(KeyCode key)
    {
        return key is KeyCode.LeftShift or KeyCode.RightShift
            or KeyCode.LeftControl or KeyCode.RightControl
            or KeyCode.LeftAlt or KeyCode.RightAlt
            or KeyCode.LeftCommand or KeyCode.RightCommand
            or KeyCode.LeftWindows or KeyCode.RightWindows;
    }

    internal static void Log(string message)
    {
        if (DebugLogging is { Value: true })
        {
            Instance.Logger.LogInfo(message);
        }
    }

    internal static void LogError(string message)
    {
        Instance.Logger.LogError(message);
    }
}
