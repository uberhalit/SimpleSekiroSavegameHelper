
using System.Security.Cryptography;
using System.Windows;

namespace SimpleSekiroSavegameHelper
{
    internal class GameData
    {
        internal const string PROCESS_NAME = "sekiro";
        internal const string PROCESS_TITLE = "Sekiro";
        internal const string PROCESS_DESCRIPTION = "Shadows Die Twice";
        internal const string PROCESS_EXE_VERSION = "1.4.0.0";
        internal static readonly string[] PROCESS_EXE_VERSION_SUPPORTED = new string[2]
        {
            "1.3.0.0",
            "1.2.0.0"
        };


        /**
            There are several MD5 checksums within the save file that get checked on load. We remove the conditional jump after compare between calculated checksum and one read from file.
            0000000141AF28C8 | 38840C 90000000               | cmp byte ptr ss:[rsp+rcx+90],al          | validate checksum
            0000000141AF28CF | 75 0E                         | jne sekiro.141AF28DF                     |
            0000000141AF28D1 | FFC3                          | inc ebx                                  |
            0000000141AF28D3 | 48:FFC1                       | inc rcx                                  |
            0000000141AF28D6 | 83FB 10                       | cmp ebx,10                               |
            0000000141AF28D9 | 72 E5                         | jb sekiro.141AF28C0                      |
         */
        internal const string PATTERN_CHECKSUMCHECK = "38 84 0C ?? ?? ?? ?? 75 0E FF ?? 48 ?? ?? 83 ?? 10 72";
        internal const int PATTERN_CHECKSUMCHECK_OFFSET = 7;
        internal static readonly byte[] PATCH_CHECKSUMCHECK_DISABLE = new byte[2] { 0x90, 0x90 }; // nop
        internal static readonly byte[] PATCH_CHECKSUMCHECK_ENABLE = new byte[2] { 0x75, 0x0E };  // jne +14


        /**
            Here game checks if save file got same SteamID as user and jumps accordingly, we replace conditional jump with unconditional one.
            0000000140DD2939 | 45:84FF                       | test r15b,r15b                           |
            0000000140DD293C | 75 07                         | jne sekiro.140DD2945                     | jumps if savegame is "valid"
            0000000140DD293E | B9 06000000                   | mov ecx,6                                |
            0000000140DD2943 | EB 3B                         | jmp sekiro.140DD2980                     |
            0000000140DD2945 | B9 07000000                   | mov ecx,7                                |
         */
        internal const string PATTERN_SAVEFILECHECK = "45 84 FF 75 ?? B9 06 00 00 00 EB ?? B9 07 00 00 00";
        internal const int PATTERN_SAVEFILECHECK_OFFSET = 3;
        internal static readonly byte[] PATCH_SAVEFILECHECK_DISABLE = new byte[1] { 0xEB }; // jmp
        internal static readonly byte[] PATCH_SAVEFILECHECK_ENABLE = new byte[1] { 0x75 };  // jne


        /**
            Here game checks if save file slot got same SteamID as user and jumps accordingly, we replace conditional jump with unconditional one.
            0000000140DD494A | 48:8B05 DF43D702              | mov rax,qword ptr ds:[143B48D30]         |
            0000000140DD4951 | 40:38B8 F0000000              | cmp byte ptr ds:[rax+F0],dil             |
            0000000140DD4958 | 75 1D                         | jne sekiro.140DD4977                     | jumps if savegame slot is "valid"
            0000000140DD495A | E8 A13DA5FF                   | call sekiro.140828700                    |
            0000000140DD495F | E8 ECDDF5FF                   | call sekiro.140D32750                    |
            0000000140DD4964 | B8 06000000                   | mov eax,6                                |
         */
        internal const string PATTERN_SAVESLOTCHECK = "48 8B 05 ?? ?? ?? ?? 40 38 B8 ?? ?? ?? ?? 75 ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? B8 06 00 00 00";
        internal const int PATTERN_SAVESLOTCHECK_OFFSET = 14;
        internal static readonly byte[] PATCH_SAVESLOTCHECK_DISABLE = new byte[1] { 0xEB }; // jmp
        internal static readonly byte[] PATCH_SAVESLOTCHECK_ENABLE = new byte[1] { 0x75 };  // jne


        /**
            Various data and offsets from a Sekiro SL2 file.
         */
        /// <summary>
        /// BND4 file type signature bytes.
        /// </summary>
        internal static readonly byte[] SEKIRO_SAVE_FORMAT_FILETYPE = new byte[8] { 0x42, 0x4E, 0x44, 0x34, 0x00, 0x00, 0x00, 0x00 };  // 0x00000000

        /// <summary>
        /// Total length of bytes used in save slot block MD5 checksum calculation as well as length of save slot without 16 bytes of checksum.
        /// </summary>
        internal const int SEKIRO_SAVE_FORMAT_SLOT_CHECKSUM_LENGTH = 0x00100000;

        /// <summary>
        /// Total length of bytes used in settings block MD5 checksum calculation.
        /// </summary>
        internal const int SEKIRO_SAVE_FORMAT_SETTINGS_CHECKSUM_LENGTH = 0x00060000;

        /// <summary>
        /// Maximum slots inside the save game file.
        /// </summary>
        internal const int SEKIRO_SAVE_FORMAT_SLOT_COUNT = 10;

        /// <summary>
        /// Total length of bytes of general game settings block.
        /// </summary>
        internal const int SEKIRO_SAVE_FORMAT_GENERAL_SETTINGS_LENGTH = 0x0000002C;

        /// <summary>
        /// Total length of bytes of user game settings block.
        /// </summary>
        internal const int SEKIRO_SAVE_FORMAT_USER_SETTINGS_LENGTH = 0x00000454;

        /// <summary>
        /// Minimal length a valid save file must have.
        /// </summary>
        internal const int SEKIRO_SAVE_FORMAT_MINIMAL_LENGTH = 0x00A603B0;

        /// <summary>
        /// Offsets within a Sekiro SL2 save game file.
        /// </summary>
        //// thanks to 'klm123' for some offsets
        internal struct SekiroSaveFormatOffsets
        {
            /// <summary>
            /// Offset of BND4 file type signature.
            /// </summary>
            internal const int fileType = 0x00000000;

            /// <summary>
            /// Offset of checksum of first save game slot, 16 bytes length, checksum bytes start right afterwards [0x00000310-0x0010030F : 0x1000000].
            /// </summary>
            internal const int firstSlotChecksum = 0x00000300;

            /// <summary>
            /// Offset of Steam Id in first save game slot.
            /// </summary>
            internal const int slotSteamId = 0x00034164;

            /// <summary>
            /// Offset of MD5 checksum of settings block, 16 bytes length, checksum bytes start right afterwards [0x00A003B0-0x00A603AF : 0x60000].
            /// </summary>
            internal const int settingsChecksum = 0x00A003A0;

            /// <summary>
            /// Offset to Steam Id used in settings block. 8 bytes length.
            /// </summary>
            internal const int settingsSteamId = 0x00A003D4;

            /// <summary>
            /// Offset to general game settings.
            /// </summary>
            internal const int generalSettings = 0x00A003DC;

            /// <summary>
            /// Offset to user game settings.
            /// </summary>
            internal const int userSettings = 0x00A029E0;
        }
    }
}
