using BepInEx.Configuration;

namespace SilkCurseMod;

/// <summary>
/// Tag object passed to <see cref="ConfigDescription"/> so that BepInEx.ConfigurationManager
/// renders a setting with a custom editor. ConfigurationManager finds this type by its simple
/// name ("ConfigurationManagerAttributes") and copies fields by name via reflection, so the
/// field names/types below must match exactly what it expects.
/// </summary>
internal sealed class ConfigurationManagerAttributes
{
    /// <summary>
    /// Custom editor that lets the user click the setting and then press a key to rebind it.
    /// </summary>
    public CustomHotkeyDrawerFunc? CustomHotkeyDrawer;

    public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);
}
