# Argon Menu API

The Argon menu is a lightweight in-game HUD for Gorilla Tag. It renders a
world-space text panel in front of the player and navigates a stack of pages.
Third-party code (the bundled `ArgonExtensions` project, or your own plugin)
registers custom root items through the public `Argon.MenuApi`.

## Input

| Input | Action |
| --- | --- |
| Right secondary (B / Y) | Open / close the menu |
| Menu Nav binding (default: right stick) | Move selection up / down |
| Right primary (A) | Activate the selected item |
| `Back` row (top of every submenu) | Go back one page |
| Right secondary (on the root page) | Close the menu |

The menu remembers your position: closing and reopening (with right secondary)
keeps the open page, and going back into a page restores the row you had
selected. Pages are rebuilt from their factories after every action, so
labels/state stay live.

## Registering a root item

```csharp
MenuApi.RegisterRootItem("BetterPlayers", () => new MenuPage("Better Players", new[]
{
    new MenuItem("Option that runs code", RunSomething),
    new MenuItem("Option that navigates deeper", () => CreateSubPage()),
    new MenuItem("Already activated", (Action)null, false, true), // disabled / grayed out via rich text
    new MenuItem("Yes, do it", DoIt, returnsToPrevious: true),    // acts like a confirmation
}));
```

Call `MenuApi.UnregisterRootItem("BetterPlayers")` when your plugin unloads.
`RegisterRootItem` is idempotent per label (re-registering replaces the item).

## Building pages

A `MenuPage` has a title and a list of `MenuItem`s:

```csharp
new MenuPage("Title", items: IEnumerable<MenuItem>)
```

A `MenuItem` can be one of the following:

| Construction | Behavior when activated |
| --- | --- |
| `new MenuItem("Label", Action action)` | Runs `action`, then refreshes the current page (labels update). |
| `new MenuItem("Label", Action action, bool returnsToPrevious)` | Runs `action`, then goes back to the previous page. Use for confirmation-like items. |
| `new MenuItem("Label", Action action, bool returnsToPrevious, bool disabled)` | Same as above, but `disabled` items cannot be selected into action (rendered gray by convention with `<color=#7f7f7f>`). |
| `new MenuItem("Label", Func<MenuPage> openPage)` | Opens `openPage()` as a submenu (a `Back` row is prepended). |

Item labels support TMP rich text (`<color=#ffc600>Gold</color>`).

## Page factories and refresh

Every entry in the menu stack keeps the `Func<MenuPage>` used to build it.
After actions, on `Back`, and on reopen, the current page is rebuilt through
that factory. **Make factories cheap and side-effect free-ish** — they may run
every time the player navigates. A factory reading config/state (e.g. current
mutes) produces a page whose labels always reflect reality.

Exceptions thrown by page factories are logged (with the ArgonMenu context)
and abort navigation for that press; the previous page remains active.

## Temporary / in-session state

Entries like "reported this player" or "muted hand taps" belong to your
extension project — persist player IDs in your own `BepInEx.Configuration`
entries and rebuild your page from that state; the menu will pick the change
up on the next refresh.

## Example: a disabled (already used) option

```csharp
bool alreadyReported = reported.Contains(key);
if (alreadyReported)
    return new MenuItem("<color=#7f7f7f>Report cheating (reported)</color>", (Action)null, false, true);
return new MenuItem("Report cheating", () => Scoreboard.Report(player, reason), true);
```

## Building

- `Argon.csproj` — core menu (BepInEx plugin `zone.xenon.argon`).
- `ArgonExtensions/ArgonExtensions.csproj` — separate plugin that adds the
  "Extensions" root page (player list, lobby hop, reports, room history).
  It is part of `Argon.slnx` on the same `Directory.Build.props` layout and
  has a hard BepInEx dependency on the main mod (load Argon first).

Both DLLs go into `BepInEx/plugins` (subfolders are fine).
