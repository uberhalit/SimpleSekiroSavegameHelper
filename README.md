# Simple Sekiro Savegame Helper

A small utility for save game management for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#. Can import save files from other users, backup, restore, set names to backups, delete and additionally patch the game's memory so it will skip all checks on save games therefore it enables you to modify your save files. Also available on [Nexus Mods](https://www.nexusmods.com/sekiro/mods/274/).

![Simple Sekiro Savegame Helper](https://camo.githubusercontent.com/2a2f906c62493531511a636a75b4305564beb04a/68747470733a2f2f692e696d6775722e636f6d2f573663345841742e706e67)

## Download

[Get the latest release here](https://github.com/uberhalit/SimpleSekiroSavegameHelper/releases)

## Features

* automatically determines latest savegame on startup
* Patch game to skip checksum and SteamID checks on load, this will enable you to load modified save games **(RAM patches only)**
* (Import foreign savegame) imports a save game from a different steam account into yours, your game settings can be preserved if a destination is given
* (Backup savegame) backup current savegame
* (Set name for selected backup) set a custom name for a backup
* (Revert to selected backup) deletes current savegame and places selected backup as new one
* (Delete selected backup) deletes selected backup form disk
* optionally hotkey shortcuts for all features
* manually select another savegame path to lock/backup

## Usage

Start the utility, select your savegame and back it up if the latest backup doesn't fit already, start Sekiro, load up savegame ingame. The patch affects the games memory, no game files will be modified. As the patcher hot-patches the RAM **you have to enable the patch every time you want to skip save file checks to import modified files.** This is not neccessary if you properly import a save file using the Import function.

### On 'Patch game to load unimported foreign/modified save files':

The game runs several checksum checks and compares Steam Ids before loading a save file. Enabling this option will patch the game to skip all checks which allows you to not only load save files from other users but also to modify your own file without worrying about checksums. Be aware however that the game does not have a settings file outside of graphical settings so loading a foreign save file will also load all game settings from that file. Consider using the Import function if you want to preserve your own game settings.

### On 'Import foreign savegame':

This function takes a source file and optionally a destination file. The save slots from the source file will be copied over to the destination file while the game settings of the destination will be preserved. Omitting the destination file will directly modify the source file to make it importable to your game. Backups will be created automatically. Be aware however that the game does not have a settings file outside of graphical settings so when you omit the destination file the newly imported save file will contain all game settings from the provider of the file.

## Prerequisites

* .NET Framework 4.5
* administrative privileges (for patching and to overwrite files)

## Building

Use Visual Studio 2017 or newer to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits

* [klm123](https://gaming.stackexchange.com/users/49789/klm123) for some save file offsets
* [Darius Dan](http://www.dariusdan.com) for the icon

## Version History

* v2.0.0.0 (2019-05-01)
  * Added feature to patch game so it will accept any save file
  * Added save file importer
  * Checkbox states will be saved now
  * Small fixes
* v1.1.0.0 (2019-04-16)
  * Added feature to give a name to backups
  * Added feature to delete backups
  * Hotkeys are toggleable now
  * Removed lock/unlock feature as they could crash the game
* v1.0.0.0 (2019-03-23)
  * Initial release
