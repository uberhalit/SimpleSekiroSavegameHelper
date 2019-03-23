# Simple Sekiro Savegame Helper

A small utility for save game management for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#.

[![Simple Sekiro Savegame Helper](data/screen01.png)](#)

## Features

* automatically determines latest savegame on startup
* (Lock savegame) prevents game from writing to current savegame so death counter and alike won't be updated
* (Unlock savegame) remove write lock from savegame so game can save current progress
* (Backup savegame) backup current savegame
* (Revert to selected backup) deletes current savegame and places selected backup as new one
* manually select another savegame to lock/backup

## Prerequisites

* .NET Framework 4.0
* administrative privileges (for locking/unlocking)

## Usage

Start the utility, select your savegame and backup if the latest ones don't fit already, start Sekiro, load up savegame ingame.

## Building

Use Visual Studio 2017 to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Known problems

* the game writes some bytes to the savegame when in main menu, this won't work if you locked the file, unlock it first, then load, then lock again

## Version History

* v1.0.0 (2019-03-23)
  * Initial release