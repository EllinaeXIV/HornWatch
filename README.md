# Hornwatch

[![Support me on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/E7C8255KNS)

A Dalamud plugin for Occult Crescent, in South Horn and North Horn.

It watches the zone and tells you when something happens.

## Requirements

| | |
|---|---|
| Dalamud | required |
| [vnavmesh](https://github.com/awgil/ffxiv_navmesh) | only for auto travel |
| [Lifestream](https://github.com/NightmareXIV/Lifestream) | optional, shortens aethernet trips |

Everything but auto travel runs on Dalamud alone.

## Install

In `/xlsettings`, open the **Experimental** tab and add this under **Custom Plugin
Repositories**:

```
https://raw.githubusercontent.com/EllinaeXIV/DalamudPlugins/main/pluginmaster.json
```

Press **+**, then **Save and Close**. Closing with the cross saves nothing.

Install **Hornwatch** from `/xlplugins` and step into the zone. `/hornwatch` opens the
window, `/hw` is shorter, and the gear icon holds the settings.

## What it watches

**Encounters.** Critical encounters and FATEs as they run, with progress and headcount, and
a game sound of your choosing when one spawns.

**Pots.** A countdown to the next treasure pot, in the window and beside your clock.

**Coffers.** Every coffer on the game's own map, a word when one is standing near you, and
an optional route that walks them for you.

**Jobs.** Your party's phantom jobs, your own 24 levels and their experience, and which
monster teaches each phantom Blue Mage spell.

Alerts and markers are kept per zone. The interface follows your client, in English or
French.

## Auto travel

Off by default. Enabling it takes two clicks: acknowledge the risk, then turn it on.

It moves your character for you, which the terms of service forbid, and it is detectable.
Nobody can promise you otherwise. Everything else in the plugin only reads.

## Notes

The pot cycle is community knowledge rather than game data, so a countdown only exists for a
turnover this client watched happen. It says N/A instead of inventing one.

Bugs go in [issues](https://github.com/EllinaeXIV/HornWatch/issues). Building it takes the
.NET 10 SDK and `dotnet build Hornwatch.slnx -c Debug`.

AGPL-3.0-or-later. See [LICENSE](LICENSE).
