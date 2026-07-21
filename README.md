# BoardStatsPlugin

A lightweight statistics overlay for **Hearthstone Battlegrounds**, designed as a plugin for **Hearthstone Deck Tracker**.

BoardStatsPlugin automatically calculates and displays the combined statistics of the minions on each board, making it easy to see the total Attack and Health at a glance during both the Recruitment and Combat phases.

> Add a screenshot or GIF here
> `![BoardStatsPlugin preview](docs/boardstats-preview.png)`

## About the Project

I created BoardStatsPlugin as a personal challenge.

I am not a professional programmer, but I have always wanted a simple way to count the total statistics of a Battlegrounds board without having to calculate everything manually.

Knowing the exact total Attack and Health of a board is obviously not essential in every game. Most of the time, it is simply fun information to have.

However, it can also become useful in certain situations, especially when approaching the final stages of a lobby. It can help players compare board strength, follow how quickly a composition is scaling, and better understand the difference between two opponents.

The main goal of this plugin is therefore simple:

**Make Battlegrounds board statistics easier to read, more visual, and more entertaining.**

## Features

* Displays the total Attack of the minions on the board.
* Displays the total Health of the minions on the board.
* Calculates statistics automatically.
* Works during the Recruitment phase.
* Works during Battlegrounds combat.
* Displays statistics for both the player and the opponent when available.
* Integrates directly into the Hearthstone Deck Tracker overlay.
* Designed specifically for Hearthstone Battlegrounds.
* Lightweight and easy to use.

## Why Use BoardStatsPlugin?

BoardStatsPlugin is not intended to replace strategy, combat simulations, or game knowledge.

Instead, it provides an additional visual indicator that can be interesting and useful during a game.

For example, it can help you:

* Compare your board with an opponent’s board.
* Follow the growth of a scaling composition.
* Quickly estimate the overall size of a board.
* Notice large statistical differences during late-game fights.
* Keep track of impressive or unusual Battlegrounds boards.
* Avoid manually adding together every minion’s statistics.

The total statistics do not tell the complete story of a fight. Minion effects, Divine Shields, Cleave, Venomous, Reborn, Deathrattles and attack order can all be more important than raw numbers.

The plugin should therefore be viewed as an informative and entertaining tool rather than a prediction system.

## For Twitch and YouTube Creators

BoardStatsPlugin can also be useful for Twitch streamers, YouTube creators and other Battlegrounds content creators.

Because the total statistics are displayed directly on the screen, viewers can immediately understand the general size and power of each board.

This can create additional entertainment during a stream or video, particularly when:

* Two very large boards fight each other.
* A composition gains a huge amount of statistics in a single turn.
* A player reaches the final stages of a lobby.
* Viewers try to predict which board will win.
* A statistically weaker board wins because of its effects or attack order.
* The audience compares different scaling strategies.

The visible totals can encourage viewers to discuss the fight, make predictions and react to unusual results.

This can help create conversations in chat and add another visual element to Battlegrounds content.

## Installation

1. Go to the [latest release page](REPLACE_WITH_YOUR_RELEASE_LINK).

2. Download `BoardStatsPlugin.dll` or the provided ZIP file.

3. Open Hearthstone Deck Tracker.

4. Open:

   `Options → Tracker → Plugins`

5. Drag and drop the downloaded DLL or ZIP file into the plugins window.

6. Restart Hearthstone Deck Tracker if necessary.

7. Make sure BoardStatsPlugin is enabled in the plugin list.

8. Launch Hearthstone and start a Battlegrounds game.

Do not download the automatically generated GitHub files named `Source code`.

These files contain the project’s source code and are not the compiled plugin required for installation.

## Updating the Plugin

To update BoardStatsPlugin:

1. Download the latest version from the [Releases page](REPLACE_WITH_YOUR_RELEASE_LINK).
2. Close Hearthstone Deck Tracker.
3. Replace the previous plugin file with the new version.
4. Restart Hearthstone Deck Tracker.

## Compatibility

* Hearthstone Battlegrounds
* Hearthstone Deck Tracker
* Windows

Compatibility may change after updates to Hearthstone or Hearthstone Deck Tracker.

Check the [Releases page](REPLACE_WITH_YOUR_RELEASE_LINK) for information about the latest tested version.

## Troubleshooting

### The plugin does not appear in Hearthstone Deck Tracker

Make sure that:

* You downloaded the DLL or ZIP from the Releases page.
* You did not download the GitHub source code archive.
* The plugin was added through the HDT plugins window.
* Hearthstone Deck Tracker was restarted.
* BoardStatsPlugin is enabled.

### Windows blocks the DLL

Windows may sometimes block DLL files downloaded from the Internet.

To unblock the file:

1. Right-click `BoardStatsPlugin.dll`.
2. Select **Properties**.
3. Enable **Unblock**, if the option is available.
4. Click **Apply**.
5. Restart Hearthstone Deck Tracker.

### The displayed statistics are incorrect

Hearthstone Battlegrounds changes regularly, and some unusual minion effects or new mechanics may require specific handling.

Please report the problem through the GitHub Issues page and include:

* Your BoardStatsPlugin version.
* Your Hearthstone Deck Tracker version.
* A screenshot of the board.
* The minions or effects involved.
* A short description of the incorrect result.

## Feedback and Bug Reports

Feedback is welcome.

As this plugin began as a personal programming challenge, reports from real players are especially useful for improving it.

To report a bug or suggest an improvement, open an issue here:

[GitHub Issues](REPLACE_WITH_YOUR_ISSUES_LINK)

When reporting a bug, please provide as much information as possible.

## Disclaimer

BoardStatsPlugin is an independent community project.

It is not affiliated with, endorsed by, or officially supported by Blizzard Entertainment, HearthSim, HSReplay.net or Hearthstone Deck Tracker.

Hearthstone and Hearthstone Battlegrounds are trademarks or registered trademarks of Blizzard Entertainment, Inc.

## Support the Project

The best ways to support BoardStatsPlugin are:

* Try the plugin.
* Report bugs.
* Suggest improvements.
* Share the project with other Battlegrounds players.
* Use it in a Twitch stream or YouTube video.
* Star the repository on GitHub.

## License

See the [`LICENSE`](LICENSE) file for more information.
