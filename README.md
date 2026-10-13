# Savefile Manager (WPF / C#)

A C# + WPF rewrite of [Green-Bat/Savefile-Manager](https://github.com/Green-Bat/Savefile-Manager).
Back up and replace save files/folders for any game using profiles.

## Build & run

Requires the .NET 8 SDK (or newer) on Windows.

```
dotnet run -c Release
# or publish a single exe:
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Or open the folder in Visual Studio 2022 and press F5.

Settings, logs and backups live in a `config` folder next to the exe (falls back to
`%AppData%\SavefileManager\config` if that folder is read-only).

## Features (same as the original)

- Profiles with a personal saves folder, the game's saves folder and a file extension (`*` = folders / no extension)
- `=>` replace the selected game save with your selected save (the game file is backed up first)
- `<=` back up the selected game save into your list (into the selected folder, or the main folder if nothing is selected)
- Right-click: Rename / Delete (a copy is kept in `config/backups/Deleted`) / Create Folder
- Double-click a path to open it in Explorer
- Batman: Arkham Knight handling (all three slots replaced; most recent slot backed up)
- Themes: Dark, Dark (Alt), Classic, Solarized, plus a fully editable **Custom** theme

## Custom theme

Themes > Edit custom theme... lets you pick a color for every part of the UI: backgrounds, text,
buttons (normal/hover/pressed/disabled), file lists, selection (focused and unfocused), scroll bars,
text boxes and text selection, the profile drop-down, menus, tooltips and the Windows 11 title bar.
Full picker (hue bar, saturation/brightness square, RGB, hex, presets), live preview,
and Import/Export to share a theme as a `.json` file.

## Using two computers

- **Profiles are never deleted automatically.** If a folder can't be found you are asked once to set it;
  pressing Cancel keeps the profile, it is just shown as unavailable on that PC (banner + disabled buttons)
  until you set the folders (Options > Set folders for this PC...).
- **Folder paths are stored per computer.** Fixing the paths on PC B does not change PC A's paths.
- If the `config` folder is shared/synced between PCs, every save first merges in what the other PC wrote,
  so profiles, paths and theme changes from both PCs are kept.
- A damaged `settings.json` is never overwritten blindly: it is copied aside and the last
  `settings.json.bak` is loaded.
- Backups are no longer purged automatically every week; use Options > Clean backups older than 7 days.

## Differences from the original

- Replace now backs up the game file *before* overwriting it (the original copied the already-replaced file).
- Rename never deletes a non-empty folder to make room; overwriting a file keeps a safety copy.
- Lists refresh with a file system watcher instead of polling every second.
- No admin elevation or update check at startup.
- "Dark (Alt)" has the same colors as "Dark" but a lighter button/list border (the original differs only in Tk's base widget style, which has no WPF equivalent).

## Fonts, backgrounds and icons

* **Themes > Fonts & backgrounds...** - pick one font for all text, or choose a separate font for
  each part (title, profile selector, paths, file lists, buttons, menus, tooltips). "Add font file..."
  loads any downloaded .ttf / .otf; a copy goes to `config/fonts`, nothing is installed into Windows.
  You can also set a picture behind each file list, with opacity and fit.
* **File icons** - right-click a file or folder > *Set icon...* / *Remove icon*.
* **Profile picture** - Options > *Set profile icon...* / *Remove profile icon*; shown in the profile selector.
* Every picture you choose is copied to `config/images`, so deleting the original changes nothing.
  Icons are stored per profile by relative path, so they follow the profile between your PCs.
