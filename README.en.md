# SilkCurseMod

[简体中文](README.md) | **English**

A Hollow Knight: Silksong (BepInEx) mod that lets you toggle the **畸芽诅咒 / Cursed state**
with a hotkey, without ever actually equipping the Cursed crest.

## Control modes

There are two hotkeys, one for each curse mode:

| Key | Default | Mode | How the curse ends |
| --- | --- | --- | --- |
| `General/Hotkey` | **F6** | Manual curse | Only by pressing the same key again. |
| `General/KillModeHotkey` | **F5** | Kill to uncurse | Only by killing enough enemies (F6 also cancels it). |

### F6 — manual curse
- If not cursed, enables the curse. It stays until you press F6 again.
- If already cursed (in either mode), pressing F6 lifts the curse.

### F5 — kill to uncurse
- Enables the curse in **kill mode**. In this mode the curse can only be lifted by killing enemies;
  pressing F6 still cancels it manually.
- Pops up a vanilla-style notification with the current progress (`0 / N`) and **resets the
  progress to `0 / N` every time F5 is pressed**.
- Each **journal-recorded enemy kill** made while cursed advances the counter. Reaching
  `Curse/KillsToUncurse` (default **10**) lifts the curse automatically.

Kill detection uses the game's own `EnemyDeathEffects.RecordKillForJournal`, which only runs for
enemies that have a journal record *and* that the hero actually damaged. Scripted deaths and
environmental kills (spikes, lava, other enemies) therefore do not count.

## What the curse does (vanilla-faithful)

- The silk spool is capped at **3** and shows the vanilla cursed spool visuals.
- Binding (healing with silk) is disabled.
- All equipped tools and skills are unequipped. Your previous loadout is remembered and restored
  when the curse ends (`Curse/DisableTools`, on by default).
- Your real crest and move set are **untouched** — the mod fakes the cursed state instead of
  switching `CurrentCrestID` to the Cursed crest.

The state (active flag, mode, kill progress and the tool backup) is stored per save profile
(`PlayerData.profileID`) in `BepInEx/config/SilkCurseMod.state.txt`, so enabling it on one save
does not affect the others.

## How it works

The game exposes a single "am I cursed" concept that fans out everywhere:

| Game member | Effect |
| --- | --- |
| `PlayerData.IsAnyCursed` | spool visuals, silk cap (`CurrentSilkMaxBasic`), bind cost, death cocoon |
| `ToolCrest.IsEquipped` (Cursed crest) | HUD frame, hero animation, `ToolItemManager.IsCursed`, floating slots |

`CursePatches` postfixes these getters to report the cursed state while the mod flag is active,
so every downstream visual/logic path turns on as in vanilla. `CurseController` additionally
empties the current crest's tool slots (because tool throwing is *not* gated on the cursed flag)
and persists everything per profile.

Because faking `ToolCrest.IsEquipped` would also leak into the game's PlayMaker crest checks,
those are excluded: the `ToolCrest.IsEquipped` fake is suppressed while `CheckIfCrestEquipped`
evaluates (and while `HeroController.Recoil` / `RecoilDown` read the crest). Without this, the
`Sprint` FSM's dash-stab reaction (`Reaction Type`) would route a Hunter-crest dash slash into the
Witch/cursed lash follow-up, making only the follow-up animation look cursed.

## Configuration

Generated at `BepInEx/config/io.github.lifelan.silkcursemod.cfg`:

| Key | Default | Meaning |
| --- | --- | --- |
| `General/Hotkey` | `F6` | Toggle key for the manual curse (`KeyCode` name). |
| `General/KillModeHotkey` | `F5` | Kill-to-uncurse mode key; also resets progress. |
| `Curse/KillsToUncurse` | `10` | Kills required to lift the curse in kill mode (1–999). |
| `Curse/DisableTools` | `true` | Unequip tools/skills while cursed. |
| `General/DebugLogging` | `false` | Extra logging. |

## Important

Because the mod actually empties the tool slots in `PlayerData` (that is the only reliable way to
stop tools from being thrown), those changes are part of the save file until the curse ends. The
backup loadout lives in `SilkCurseMod.state.txt`; do not delete that file while cursed.

## Building

```sh
dotnet build -c Release
```

The build copies `SilkCurseMod.dll` into `BepInEx/plugins/lifelan-SilkCurseMod/` (paths configured
in `SilksongPath.props`).
