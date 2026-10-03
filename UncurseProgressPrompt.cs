using System;
using GlobalSettings;
using UnityEngine;

namespace SilkCurseMod;

/// <summary>
/// Shows kill-to-uncurse progress using the game's own achievement popup prefab so the prompt
/// matches the vanilla notification look (fade in / hold / fade out, handled by
/// <see cref="AchievementPopup"/> itself).
///
/// Notes about that prefab (reverse engineered): it only has an icon + a single name text, its
/// description text is unassigned, and its built-in sound is played at world origin through a 3D
/// source. So we fill the icon with the cursed crest sprite, put the whole message in the name
/// text, and force the game's 2D UI audio source + a UI sound event.
/// </summary>
internal static class UncurseProgressPrompt
{
    private static AchievementPopup? _popup;

    internal static void ShowProgress(int kills, int required)
    {
        Show($"诅咒 · {kills} / {required}");
    }

    internal static void ShowUncursed()
    {
        Show("诅咒已解除");
    }

    private static void Show(string text)
    {
        try
        {
            var handler = AchievementPopupHandler.Instance;
            var template = handler != null ? handler.template : null;
            if (template == null)
            {
                return;
            }

            // A popup destroyed by a scene change compares equal to null, so this recreates it.
            if (_popup == null)
            {
                _popup = UnityEngine.Object.Instantiate(template, template.transform.parent);
            }

            var icon = GetIcon();

            // The template's own sound plays at world origin through a 3D source and is inaudible.
            // Force the game's 2D UI audio source and a UI sound event instead.
            _popup.audioPlayerPrefab = Audio.DefaultUIAudioSourcePrefab;
            _popup.sound = UI.ItemQuestMaxPopupSound;

            _popup.gameObject.SetActive(true);
            _popup.Setup(icon, text, string.Empty);

            // The prefab has no description text, so hide the icon slot entirely if we have no icon.
            if (_popup.image != null)
            {
                _popup.image.enabled = icon != null;
            }
        }
        catch (Exception e)
        {
            SilkCurseModPlugin.LogError("Failed to show uncurse prompt: " + e.Message);
        }
    }

    private static Sprite? GetIcon()
    {
        try
        {
            var crest = Gameplay.CursedCrest;
            return crest != null ? crest.CrestSprite : null;
        }
        catch
        {
            return null;
        }
    }
}
