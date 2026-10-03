using HarmonyLib;
using HutongGames.PlayMaker.Actions;

namespace SilkCurseMod;

/// <summary>
/// Harmony patches that make the game behave as if the cursed crest were equipped,
/// without ever touching <see cref="PlayerData.CurrentCrestID"/>.
/// </summary>
[HarmonyPatch]
internal static class CursePatches
{
    /// <summary>
    /// The game derives the cursed state everywhere from this property
    /// (spool visuals, silk cap, bind cost, death cocoon). Force it on while active.
    /// </summary>
    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.IsAnyCursed), MethodType.Getter)]
    [HarmonyPostfix]
    private static void PlayerData_IsAnyCursed_Postfix(PlayerData __instance, ref bool __result)
    {
        if (CurseController.Active && CurseController.IsLivePlayerData(__instance))
        {
            __result = true;
        }
    }

    /// <summary>
    /// Make the Cursed crest report itself as equipped so the HUD frame, hero animation,
    /// floating tool slots and <see cref="ToolItemManager.IsCursed"/> all turn on,
    /// while the player keeps their real crest and move set.
    /// </summary>
    [HarmonyPatch(typeof(ToolCrest), nameof(ToolCrest.IsEquipped), MethodType.Getter)]
    [HarmonyPostfix]
    private static void ToolCrest_IsEquipped_Postfix(ToolCrest __instance, ref bool __result)
    {
        if (!__result
            && CurseController.Active
            && !CurseController.SuppressFakeCursedEquipped
            && CurseController.IsCursedCrest(__instance))
        {
            __result = true;
        }
    }

    /// <summary>
    /// PlayMaker crest checks read <see cref="ToolCrest.IsEquipped"/> directly. The hero's Sprint
    /// FSM uses them to branch the dash stab ("dash slash") follow-up into the Witch/cursed lash
    /// when <c>WitchCrest || CursedCrest</c> is equipped. Faking the Cursed crest as equipped would
    /// therefore turn a Hunter-crest dash slash into the cursed moveset even though the hit itself
    /// looked right. Suppress the fake for the duration of every crest check so the FSMs always see
    /// the player's real crest.
    /// </summary>
    [HarmonyPatch(typeof(CheckIfCrestEquipped), nameof(CheckIfCrestEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPrefix]
    private static void CheckIfCrestEquipped_Prefix()
    {
        CurseController.PushSuppressFakeCursedEquipped();
    }

    [HarmonyPatch(typeof(CheckIfCrestEquipped), nameof(CheckIfCrestEquipped.IsTrue), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CheckIfCrestEquipped_Postfix()
    {
        CurseController.PopSuppressFakeCursedEquipped();
    }

    /// <summary>
    /// The game applies the Witch crest's recoil (used by the dash slash and down slash) whenever
    /// <c>WitchCrest.IsEquipped || CursedCrest.IsEquipped</c>. Faking the Cursed crest as equipped
    /// would leak into those attack paths and turn a Hunter-crest dash slash into the Witch one.
    /// Temporarily disable the fake while these methods read the crest state.
    /// </summary>
    [HarmonyPatch(typeof(HeroController), "Recoil", new[] { typeof(bool), typeof(bool) })]
    [HarmonyPrefix]
    private static void HeroController_Recoil_Prefix()
    {
        CurseController.PushSuppressFakeCursedEquipped();
    }

    [HarmonyPatch(typeof(HeroController), "Recoil", new[] { typeof(bool), typeof(bool) })]
    [HarmonyPostfix]
    private static void HeroController_Recoil_Postfix()
    {
        CurseController.PopSuppressFakeCursedEquipped();
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilDown))]
    [HarmonyPrefix]
    private static void HeroController_RecoilDown_Prefix()
    {
        CurseController.PushSuppressFakeCursedEquipped();
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilDown))]
    [HarmonyPostfix]
    private static void HeroController_RecoilDown_Postfix()
    {
        CurseController.PopSuppressFakeCursedEquipped();
    }

    /// <summary>
    /// Re-apply the curse after every scene load / respawn.
    /// </summary>
    [HarmonyPatch(typeof(HeroController), "SceneInit")]
    [HarmonyPostfix]
    private static void HeroController_SceneInit_Postfix()
    {
        CurseController.OnHeroReady();
    }

    /// <summary>
    /// Detect which save slot was loaded so the curse stays bound to a single save profile.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "SetLoadedGameData", new[] { typeof(SaveGameData), typeof(int) })]
    [HarmonyPostfix]
    private static void GameManager_SetLoadedGameData_Postfix(int saveSlot)
    {
        CurseController.SyncProfile(saveSlot);
    }

    /// <summary>
    /// Count journal-recorded enemy kills while cursed, for the kill-to-uncurse feature.
    /// <see cref="EnemyDeathEffects.RecordKillForJournal"/> only runs for enemies that have a
    /// journal record and that the hero actually damaged, so it is the game's own "player kill"
    /// signal (scripted/environmental kills do not count).
    /// </summary>
    [HarmonyPatch(typeof(EnemyDeathEffects), nameof(EnemyDeathEffects.RecordKillForJournal))]
    [HarmonyPostfix]
    private static void EnemyDeathEffects_RecordKillForJournal_Postfix()
    {
        CurseController.RegisterEnemyKill();
    }
}
