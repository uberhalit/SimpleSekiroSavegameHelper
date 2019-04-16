# Simple Sekiro Savegame Helper

A small utility for save game management for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#.

![Simple Sekiro Savegame Helper](https://camo.githubusercontent.com/498889bf84c4ea5d0c58f93355b40c82f45304c8/68747470733a2f2f692e696d6775722e636f6d2f33417752777a472e706e67)

## Download

[Get the latest release here](https://github.com/uberhalit/SimpleSekiroSavegameHelper/releases)

## Features

* automatically determines latest savegame on startup
* (Backup savegame) backup current savegame
* (Set name for selected backup) saet a custom name for a backup
* (Revert to selected backup) deletes current savegame and places selected backup as new one
* (Delete selected backup) deletes selected backup form disk
* optionally hotkey shortcuts for all features
* manually select another savegame path to lock/backup

## Prerequisites

* .NET Framework 4.5
* administrative privileges (to overwrite files and set hotkeys)

## Usage

Start the utility, select your savegame and backup if the latest ones don't fit already, start Sekiro, load up savegame ingame.

## Building

Use Visual Studio 2017 or newer to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits

* [Darius Dan](http://www.dariusdan.com) for the icon

## Version History

* v1.1.0.0 (2019-04-16)
  * Added feature to give a name to backups
  * Added feature to delete backups
  * Hotkeys are toggeable now
  * Removed lock/unlock feature as they could crash the game
* v1.0.0.0 (2019-03-23)
  * Initial release
