# Hornwatch

A Dalamud plugin for Occult Crescent, the field operation in South Horn and North Horn.

## What it is for

Occult Crescent asks you to keep an eye on several things at once. Critical encounters and
FATEs pop somewhere on a large map and are gone before long. Treasure pots run on a cycle
nobody tells you about. Your phantom job progress lives behind a menu, and so does everyone
else's. Coffers are scattered across the zone with nothing on the map to point at them.

Hornwatch puts all of that in one window and tells you when something starts, so you can
play the zone instead of watching the corner of your screen. Everything it shows is read
from the game itself, which means it works offline, in your own language, and it is current
the moment the game is.

The plugin is useful with nothing else installed. The travel features are the only part that
needs help from other plugins, and they are optional and off by default.

## Installing

Hornwatch is not in the official plugin list, so you add my repository once and then it
behaves like any other plugin.

1. In game, type `/xlsettings` to open the Dalamud settings.
2. Go to the **Experimental** tab and find **Custom Plugin Repositories**.
3. Paste this URL in the empty field at the bottom of the list:

   ```
   https://raw.githubusercontent.com/EllinaeXIV/DalamudPlugins/main/pluginmaster.json
   ```

4. Click the **+** button on the right so the URL is really added to the list, then click
   **Save and Close**. Closing the window with the cross saves nothing, and this is the step
   people usually miss.
5. Open the Plugin Installer with `/xlplugins`, search for **Hornwatch**, and install it.
6. Type `/hornwatch` to open the main window. `/hw` does the same thing.

Updates arrive through the same channel from then on. Dalamud checks the repository by
itself, so there is nothing else to do.

## Optional plugins

Hornwatch runs fine on its own. These two only matter if you want auto travel, and nothing
breaks if you skip them.

| Plugin | What it adds |
| --- | --- |
| [vnavmesh](https://github.com/awgil/ffxiv_navmesh) | Pathfinding. Without it, auto travel stays disabled and the settings tell you so. |
| [Lifestream](https://github.com/NightmareXIV/Lifestream) | Aethernet hops. Without it Hornwatch drives the shards itself, which works and is a little slower. |

The settings have a dependency panel that shows which of the two the plugin can see right
now, so you never have to guess whether something is wired up.

## What is in the window

### Watch

Every critical encounter and FATE currently up, with the number of players on it and how far
along it is. New ones appear the moment they spawn.

Each row carries two buttons: place a map flag on it, or travel there if auto travel is
turned on. When a route is running, the row tells you which leg it is on, from summoning the
mount to returning to base camp.

This tab also holds the treasure pot countdowns. Pots run one every thirty minutes,
alternating between the two spawn points of the zone. Neither the interval nor the
alternation is written anywhere in the game, so the countdown only exists for a turnover this
client actually saw. When you have just logged in it says N/A rather than inventing a number.

### Treasure hunt

The coffers, pots, second chance points, bunnies and survey points of the zone, drawn
directly on the game's own map as native markers rather than an overlay painted on top.

A small button column sits on the right edge of the map window and toggles each category
without opening the settings. It follows the map when you move or resize it. If you would
rather not have it, one checkbox hides the strip and keeps the markers.

Coffer alerts fire when a coffer comes within range of you. This watches what is actually
standing in the world, not the list of places a coffer can appear, which is the difference
between being told about a chest and being told about empty ground. You choose any
combination of an on screen toast, a chat line and a map flag. The chat line is a real map
link, so clicking it puts a flag on the coffer.

The coffer route plans a tour of the coffers and walks it for you, one at a time, with Start,
Pause, Resume, Skip and Stop. You pick which rarities to include, whether to go underground,
and whether to return to camp at the end. Two areas of North Horn killed an unattended run,
so they are excluded unless you say otherwise, and the checkbox says as much.

### Party

Everyone in your party with their real job and their phantom job.

Their phantom job level is not shown, and that is not an oversight. The game never sends it
to the client. The phantom job itself is readable only because it happens to be a status
effect on the character. The level is not part of it.

### My jobs

Your 24 phantom jobs with their level and experience, plus your knowledge, silver and gold.
This one needs you to be inside the zone, since that is the only place the data exists.

### Guides

How each phantom Blue Mage spell is unlocked, with the monster, its level, the area and its
coordinates.

It is a guide and not a tracker. The game does not tell plugins which spells you already
know, so nothing here ticks itself off. Inventing a check mark would be worse than leaving
it blank.

## Alerts

Critical encounters, FATEs, pot FATEs and raids each get their own alert, with a game sound
you pick from the sixteen the client ships and an optional chat line. A test button plays
the sound so you can choose without waiting for a spawn.

Alerts are configured per zone. South Horn and North Horn have their own tab in the settings,
and what you toggle in one has no effect on the other. The same is true of the treasure
markers and the map strip.

## Auto travel

Auto travel is off by default and takes two deliberate clicks to enable. You acknowledge the
risk, then you turn the feature on.

Read this part before you do. Auto travel moves your character for you. That is automation,
it is what the terms of service forbid, and it is detectable. Nobody can promise you
otherwise. Turn it on only if you accept that risk yourself, or leave it off and use the rest
of the plugin, which never touches your character.

With it on, a trip is a sequence of legs and each one is optional:

* Return to base camp when that is faster than walking.
* Take the aethernet to the point nearest your destination.
* Summon your mount, either a specific one you choose or the mount roulette.
* Run the rest of the way and dismount on arrival.

Every leg degrades on its own. No Lifestream means it walks further, no mount means it walks
slower, a Return refused in combat means it walks from where it stands. Only the pathfinding
is load bearing. If the target disappears while you are on your way, the trip is abandoned
and turned into a return to camp, and unloading the plugin stops any route immediately.

There is also a route overlay that draws the path in the world so you can see where it
intends to take you before it gets there.

## Appearance and language

Windows are themed from the game's own interface colours, so they match your HUD rather than
approximating it. You can follow the game's theme automatically or pick one explicitly, with
live swatches to compare.

The interface is available in English and French, following the client by default, with an
explicit override if you want something else. Only the plugin's own text is translated. Job
names, place names and monster names come from the game and are therefore already in your
language.

## Commands

| Command | Effect |
| --- | --- |
| `/hornwatch` | Open or close the main window. |
| `/hw` | The same, shorter. |

The settings are on the gear icon next to Hornwatch in the Plugin Installer, and on the
button in the main window.

## What it deliberately does not do

A few things get asked for and are not possible. They are listed here so you know they were
looked into rather than forgotten.

* **Other players' phantom job level.** Not sent to the client at all.
* **Which Blue Mage spells you know.** Not exposed to plugins.
* **A pot countdown right after you log in.** The cycle is community knowledge, not game
  data, so the plugin can only count from a turnover it witnessed.

## Building from source

You need the .NET 10 SDK and a working Dalamud dev install.

```powershell
dotnet build Hornwatch.slnx -c Debug
```

The output lands in `Hornwatch\bin\x64\Debug\`. Add that folder in `/xlsettings` under
**Experimental**, in **Dev Plugin Locations**, save and close, and the plugin appears in the
installer.

## Bugs

Open an issue on [the repository](https://github.com/EllinaeXIV/HornWatch/issues). If it
stopped working right after a patch, say which patch. Almost everything that a patch can
break lives in the code that reads the zone, and that is the first place I look.

## License

AGPL-3.0-or-later. See [LICENSE](LICENSE).
