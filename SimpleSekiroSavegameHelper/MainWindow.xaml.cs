using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace SimpleSekiroSavegameHelper
{
    public partial class MainWindow : Window
    {
        internal string _filePath = null;
        internal SettingsService _settingsService;
        internal MD5 _md5Provider;

        internal Process _gameProc;
        internal IntPtr _gameAccessHwnd = IntPtr.Zero;
        internal long _offset_checksumcheck = 0x0;
        internal long _offset_savefilecheck = 0x0;
        internal long _offset_saveslotcheck = 0x0;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On window loaded.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var mutex = new Mutex(true, "simpleSekiroSavegameHelper", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("Another instance is already running!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            GC.KeepAlive(mutex);

            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _filePath = GetLatestSaveGame();
            if (_filePath == null)
                MessageBox.Show("Could not find default save file!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
            if (!OpenDefaultSaveFile(_filePath))
                Environment.Exit(0);

            _md5Provider = MD5.Create();
        }

        /// <summary>
        /// On window closing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveConfiguration();
            _md5Provider.Clear();
            _md5Provider.Dispose();
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcherThreadFilterMessage;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, 9001);
            UnregisterHotKey(hwnd, 9002);
            UnregisterHotKey(hwnd, 9003);
            UnregisterHotKey(hwnd, 9004);
            if (_gameAccessHwnd != IntPtr.Zero)
                CloseHandle(_gameAccessHwnd);
        }

        /// <summary>
        /// Load all saved settings from previous run.
        /// </summary>
        private void LoadConfiguration()
        {
            _settingsService = new SettingsService(Path.GetDirectoryName(_filePath) + @"\SimpleSekiroSavegameHelper.xml");
            if (!_settingsService.Load()) return;
            this.cbPatchGame.IsChecked = _settingsService.ApplicationSettings.cbPatchGame;
            this.cbHotkeys.IsChecked = _settingsService.ApplicationSettings.cbHotkeys;
        }

        /// <summary>
        /// Save all settings to configuration file.
        /// </summary>
        private void SaveConfiguration()
        {
            if (_settingsService == null) return;
            _settingsService.ApplicationSettings.cbPatchGame = this.cbPatchGame.IsChecked == true;
            _settingsService.ApplicationSettings.cbHotkeys = this.cbHotkeys.IsChecked == true;
            _settingsService.Save();
        }

        /// <summary>
        /// Resets GUI and clears configuration file.
        /// </summary>
        private void ClearConfiguration()
        {
            this.cbPatchGame.IsChecked = false;
            this.cbHotkeys.IsChecked = false;
            _settingsService.Clear();
        }

        /// <summary>
        /// Windows Message queue (Wndproc) to catch HotKeyPressed.
        /// </summary>
        private void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (!handled)
            {
                if (msg.message == WM_HOTKEY_MSG_ID)    // hotkeyevent
                {
                    if (msg.wParam.ToInt32() == 9001)  // backup savegame
                    {
                        handled = true;
                        BackupSaveGameFile();
                    }
                    else if (msg.wParam.ToInt32() == 9002)  // name savegame
                    {
                        handled = true;
                        NameSaveGameFile();
                    }
                    else if (msg.wParam.ToInt32() == 9003)  // revert to savegame
                    {
                        handled = true;
                        RevertSaveGameFile();
                    }
                    else if (msg.wParam.ToInt32() == 9004)  // delete savegame
                    {
                        handled = true;
                        DeleteSaveGameFile();
                    }
                }
            }
        }

        /// <summary>
        /// Finds path to most recent save game.
        /// </summary>
        /// <returns>The path to the most recent save game.</returns>
        private string GetLatestSaveGame()
        {
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sekiro");
            if (!Directory.Exists(defaultPath))
                return null;

            string[] filePaths = Directory.GetFiles(defaultPath, "S0000.sl2", SearchOption.AllDirectories);
            string latestSavegamePath = null;
            DateTime latestSavegameDate = new DateTime(1900, 1, 1);
            foreach (var filePath in filePaths)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.LastWriteTime > latestSavegameDate)
                {
                    latestSavegamePath = filePath;
                    latestSavegameDate = fileInfo.LastWriteTime;
                }
            }
            return latestSavegamePath;
        }

        /// <summary>
        /// Collects all backups from a save game.
        /// </summary>
        /// <param name="savePath">The path to the save game.</param>
        /// <returns>A dictionary containing filepaths and creation times of backups.</returns>
        private Dictionary<string, string> GetBackupsToSaveGame(string savePath)
        {
            Dictionary<string, string> backups = new Dictionary<string, string>();
            string[] filePaths = Directory.GetFiles(Path.GetDirectoryName(savePath), "*.bak", SearchOption.TopDirectoryOnly);
            foreach (var filePath in filePaths)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                backups.Add(filePath, fileInfo.CreationTime.ToString("MM/dd HH:mm:ss"));
            }
            return backups;
        }

        /// <summary>
        /// Opens default save file and loads all saved data.
        /// </summary>
        /// <param name="path">The default save file path.</param>
        /// <returns></returns>
        private bool OpenDefaultSaveFile(string path = null)
        {
            string newPath = path;
            if (string.IsNullOrEmpty(newPath))
            {
                newPath = OpenFile("Select new S0000.sl2", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sekiro"), new[] { "S0000.sl2" }, new[] { "Sekiro Save Game File" }, true);
                if (string.IsNullOrEmpty(newPath)) return false;
            }

            _filePath = newPath;
            LoadConfiguration();
            this.tbFile.Text = _filePath;
            this.tbFile.Focus();
            this.tbFile.Select(tbFile.Text.Length, 0);
            Dictionary<string, string> backups = GetBackupsToSaveGame(_filePath);
            this.cbBackups.Items.Clear();
            foreach (KeyValuePair<string, string> backup in backups)
            {
                if (!_settingsService.ApplicationSettings.names.ContainsKey(backup.Key))
                    _settingsService.ApplicationSettings.names.Add(backup.Key, "");
                string name = _settingsService.ApplicationSettings.names[backup.Key];
                this.cbBackups.Items.Add(new KeyValuePair<string, string>(backup.Key, backup.Value + (name != "" ? " (" + name + ")" : "")));
            }
            string[] dictKeys = _settingsService.ApplicationSettings.names.Keys.ToArray();
            foreach (string name in dictKeys)
            {
                if (!backups.ContainsKey(name))
                    _settingsService.ApplicationSettings.names.Remove(name);
            }
            this.cbBackups.SelectedIndex = this.cbBackups.Items.Count - 1;
            this.tbBackupName.Text = "";
            return true;
        }

        /// <summary>
        /// Open game process for access.
        /// </summary>
        /// <returns></returns>
        private bool OpenGame()
        {
            if (_gameProc != null && _gameAccessHwnd != IntPtr.Zero && !_gameProc.HasExited) return true;

            _gameProc = null;
            if (_gameAccessHwnd != IntPtr.Zero)
            {
                CloseHandle(_gameAccessHwnd);
                _gameAccessHwnd = IntPtr.Zero;
            }
            _offset_checksumcheck = 0x0;
            _offset_savefilecheck = 0x0;
            _offset_saveslotcheck = 0x0;

            Process[] procList = Process.GetProcessesByName(GameData.PROCESS_NAME);
            if (procList.Length < 1)
            {
                MessageBox.Show("Game not running!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            int gameIndex = -1;
            for (int i = 0; i < procList.Length; i++)
            {
                if (procList[i].MainWindowTitle != GameData.PROCESS_TITLE || !procList[i].MainModule.FileVersionInfo.FileDescription.Contains(GameData.PROCESS_DESCRIPTION))
                    continue;
                gameIndex = i;
                break;
            }
            if (gameIndex < 0)
            {
                MessageBox.Show("Game not running!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
            _gameProc = procList[gameIndex];
            _gameAccessHwnd = OpenProcess(PROCESS_ALL_ACCESS, false, (uint)procList[gameIndex].Id);
            if (_gameAccessHwnd == IntPtr.Zero || _gameProc.MainModule.BaseAddress == IntPtr.Zero)
            {
                MessageBox.Show("No access to game!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                if (_gameAccessHwnd != IntPtr.Zero)
                {
                    CloseHandle(_gameAccessHwnd);
                    _gameAccessHwnd = IntPtr.Zero;
                }
                return false;
            }
            string gameFileVersion = FileVersionInfo.GetVersionInfo(procList[0].MainModule.FileName).FileVersion;
            if (gameFileVersion != GameData.PROCESS_EXE_VERSION && Array.IndexOf(GameData.PROCESS_EXE_VERSION_SUPPORTED, gameFileVersion) < 0 && !_settingsService.ApplicationSettings.gameVersionNotify)
            {
                MessageBox.Show(string.Format("Unknown game version '{0}'.\nSome functions might not work properly or even crash the game. " +
                                              "Check for updates on this utility regularly following the link at the bottom.", gameFileVersion), "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Warning);
                ClearConfiguration();
                _settingsService.ApplicationSettings.gameVersionNotify = true;
                return false;
            }
            else
                _settingsService.ApplicationSettings.gameVersionNotify = false;

            return true;
        }

        /// <summary>
        /// Scans game memory for patterns.
        /// </summary>
        /// <returns></returns>
        private bool ScanGame()
        {
            if (_gameProc == null || _gameAccessHwnd == IntPtr.Zero || _gameProc.HasExited) return false;

            if (_offset_savefilecheck != 0x0 && _offset_saveslotcheck != 0x0 && _offset_checksumcheck != 0x0)
                return true;

            PatternScan patternScan = new PatternScan(_gameAccessHwnd, _gameProc.MainModule);

            _offset_checksumcheck = patternScan.FindPattern(GameData.PATTERN_CHECKSUMCHECK) + GameData.PATTERN_CHECKSUMCHECK_OFFSET;
            Debug.WriteLine("checksum check check found at: 0x" + _offset_checksumcheck.ToString("X"));
            if (!IsValidAddress(_offset_checksumcheck))
            {
                MessageBox.Show("Could not find checksum check in memory!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                _offset_checksumcheck = 0x0;
                return false;
            }

            _offset_savefilecheck = patternScan.FindPattern(GameData.PATTERN_SAVEFILECHECK) + GameData.PATTERN_SAVEFILECHECK_OFFSET;
            Debug.WriteLine("save file check found at: 0x" + _offset_savefilecheck.ToString("X"));
            if (!IsValidAddress(_offset_savefilecheck))
            {
                MessageBox.Show("Could not find save file check in memory!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                _offset_savefilecheck = 0x0;
                return false;
            }

            _offset_saveslotcheck = patternScan.FindPattern(GameData.PATTERN_SAVESLOTCHECK) + GameData.PATTERN_SAVESLOTCHECK_OFFSET;
            Debug.WriteLine("save slot check found at: 0x" + _offset_saveslotcheck.ToString("X"));
            if (!IsValidAddress(_offset_saveslotcheck))
            {
                MessageBox.Show("Could not find save slot check in memory!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                _offset_saveslotcheck = 0x0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Patches the game so it will accept save files that aren't "valid".
        /// </summary>
        private void PatchSaveFileCheck()
        {
            if (!OpenGame() || !ScanGame())
            {
                this.cbPatchGame.IsEnabled = false;
                this.cbPatchGame.IsChecked = false;
                this.cbPatchGame.IsEnabled = true;
                return;
            }

            if (this.cbPatchGame.IsChecked == true)
            {
                WriteBytes(_gameAccessHwnd, _offset_checksumcheck, GameData.PATCH_CHECKSUMCHECK_DISABLE);
                WriteBytes(_gameAccessHwnd, _offset_savefilecheck, GameData.PATCH_SAVEFILECHECK_DISABLE);
                WriteBytes(_gameAccessHwnd, _offset_saveslotcheck, GameData.PATCH_SAVESLOTCHECK_DISABLE);
            }
            else
            {
                WriteBytes(_gameAccessHwnd, _offset_checksumcheck, GameData.PATCH_CHECKSUMCHECK_ENABLE);
                WriteBytes(_gameAccessHwnd, _offset_savefilecheck, GameData.PATCH_SAVEFILECHECK_ENABLE);
                WriteBytes(_gameAccessHwnd, _offset_saveslotcheck, GameData.PATCH_SAVESLOTCHECK_ENABLE);
            }
        }

        /// <summary>
        /// Imports a foreign savegame file.
        /// </summary>
        private void ImportSaveGameFile()
        {
            string srcPath = OpenFile("Select savegame to import from (source)", Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), new[] { ".sl2", ".bak" }, new[] { "Sekiro Save Game File", "Sekiro Save Game File Backup" });
            if (string.IsNullOrEmpty(srcPath))
                return;

            string destPath = OpenFile("Select savegame to import to (destination)", Directory.GetParent(_filePath).FullName, new[] { ".sl2", ".bak" }, new[] { "Sekiro Save Game File", "Sekiro Save Game File Backup" });
            if (string.IsNullOrEmpty(destPath))
            {
                MessageBoxResult result = MessageBox.Show(string.Format("You did not select a destination to copy the save game slots to.\n\n'{0}' will be modified to match your game but it will retain the foreign settings.\n\n" +
                                                                        "If you load it, your game settings will be overwritten with the settings from foreign source file.\n\nContinue anyways?", srcPath), "Simple Sekiro Savegame Helper", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                    return;
                destPath = "";
            }
            if (srcPath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Source file can not be destination file!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result2 = MessageBox.Show(string.Format("Importing save slots from source:\n'{0}'\n to destination:\n'{1}'\n\n" +
                                                                    "This will overwrite all save slots in destination.", srcPath, destPath), "Simple Sekiro Savegame Helper", MessageBoxButton.OKCancel, MessageBoxImage.Information);
            if (result2 != MessageBoxResult.OK)
                return;

            if (PrepareSaveFiles(srcPath, destPath))
            {
                MessageBox.Show("Import finished!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Modifies a savegame file so it will be loaded correctly on current machine.
        /// </summary>
        /// <param name="srcPath">The source file to use the save slots from.</param>
        /// <param name="dstPath">The destination file which's slots will be overwritten, provides game settings.</param>
        /// <returns></returns>
        private bool PrepareSaveFiles(string srcPath, string dstPath = null)
        {
            byte[] steamId = new byte[8];
            if (string.IsNullOrEmpty(dstPath))
            {
                string latestSave = GetLatestSaveGame();
                if (latestSave == null)
                {
                    MessageBox.Show("Could not determine Steam Id, try selecting a destination file.", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                string sSteamId = Directory.GetParent(latestSave).Name;
                if (!UInt64.TryParse(sSteamId, out ulong uSteamId))
                {
                    MessageBox.Show("Could not determine Steam Id, try selecting a destination file.", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                steamId = BitConverter.GetBytes(uSteamId);
                if (!ModifySaveFile(srcPath, steamId))
                    MessageBox.Show("An error occured while trying to import the save file.", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                byte[] dstSave = File.ReadAllBytes(dstPath);
                if (dstSave.Length < GameData.SEKIRO_SAVE_FORMAT_MINIMAL_LENGTH || !ArrayEquals(in dstSave, in GameData.SEKIRO_SAVE_FORMAT_FILETYPE, GameData.SEKIRO_SAVE_FORMAT_FILETYPE.Length, GameData.SekiroSaveFormatOffsets.fileType, 0))
                {
                    MessageBox.Show("Destination file invalid!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                steamId = CreateArrayFromArraySelection(in dstSave, GameData.SekiroSaveFormatOffsets.settingsSteamId, 8);  // read Steam Id from settings block
                if (!ModifySaveFile(srcPath, steamId, dstPath))
                    MessageBox.Show("An error occured while trying to import the save file.", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                dstSave = null;
            }

            return true;
        }

        /// <summary>
        /// Modifies a save file so it can be loaded into user's game.
        /// </summary>
        /// <param name="filePath">The file path to the save to modify.</param>
        /// <param name="steamId">The Steam Id of the user.</param>
        /// <param name="dstPath">An optional path to copy the modified file to.</param>
        /// <returns></returns>
        private bool ModifySaveFile(string filePath, byte[] steamId, string dstPath = null)
        {
            byte[] saveData = File.ReadAllBytes(filePath);
            if (saveData.Length < GameData.SEKIRO_SAVE_FORMAT_MINIMAL_LENGTH || !ArrayEquals(in saveData, in GameData.SEKIRO_SAVE_FORMAT_FILETYPE, GameData.SEKIRO_SAVE_FORMAT_FILETYPE.Length, GameData.SekiroSaveFormatOffsets.fileType, 0))
            {
                MessageBox.Show("Source file invalid!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            byte[] emptyUInt64 = new byte[8] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            for (int i = 0; i < GameData.SEKIRO_SAVE_FORMAT_SLOT_COUNT; i++)    // handle all 10 save slots
            {
                int slotSteamIdOffset = GameData.SekiroSaveFormatOffsets.slotSteamId + (i * (GameData.SEKIRO_SAVE_FORMAT_SLOT_CHECKSUM_LENGTH + 16));
                if (!ArrayEquals(saveData, emptyUInt64, 8, slotSteamIdOffset, 0))   // read Steam Id from save slot and check if not empty to determine if save slot is used
                {
                    CopyArrayToArray(ref steamId, ref saveData, 8, 0, slotSteamIdOffset);   // overwrite old Steam Id with new one
                    int slotChecksumOffset = GameData.SekiroSaveFormatOffsets.firstSlotChecksum + (i * (GameData.SEKIRO_SAVE_FORMAT_SLOT_CHECKSUM_LENGTH + 16));
                    byte[] newChecksum = CalculateMd5Checksum(in saveData, slotChecksumOffset + 16, GameData.SEKIRO_SAVE_FORMAT_SLOT_CHECKSUM_LENGTH);  // calculate new hash from save slot
                    if (newChecksum == null)
                    {
                        MessageBox.Show("Error while calculating hashes!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                    CopyArrayToArray(ref newChecksum, ref saveData, 16, 0, slotChecksumOffset); // overwrite old hash with new one
                }
            }

            if (dstPath != null) // copy destination settings block to source
            {
                byte[] dstSave = File.ReadAllBytes(dstPath);
                byte[] dstGeneralSettings = CreateArrayFromArraySelection(in dstSave, GameData.SekiroSaveFormatOffsets.generalSettings, GameData.SEKIRO_SAVE_FORMAT_GENERAL_SETTINGS_LENGTH);
                byte[] dstUserSettings = CreateArrayFromArraySelection(in dstSave, GameData.SekiroSaveFormatOffsets.userSettings, GameData.SEKIRO_SAVE_FORMAT_USER_SETTINGS_LENGTH);
                CopyArrayToArray(ref dstGeneralSettings, ref saveData, dstGeneralSettings.Length, 0, GameData.SekiroSaveFormatOffsets.generalSettings);
                CopyArrayToArray(ref dstUserSettings, ref saveData, dstUserSettings.Length, 0, GameData.SekiroSaveFormatOffsets.userSettings);
                dstSave = null;
            }

            // handle settings block
            CopyArrayToArray(ref steamId, ref saveData, 8, 0, GameData.SekiroSaveFormatOffsets.settingsSteamId);    // overwrite old Steam Id with new one
            byte[] newSettingsChecksum = CalculateMd5Checksum(in saveData, GameData.SekiroSaveFormatOffsets.settingsChecksum + 16, GameData.SEKIRO_SAVE_FORMAT_SETTINGS_CHECKSUM_LENGTH);  // calculate new hash from settings block
            if (newSettingsChecksum == null)
            {
                MessageBox.Show("Error while calculating hashes!", "Simple Sekiro Savegame Helper", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            CopyArrayToArray(ref newSettingsChecksum, ref saveData, 16, 0, GameData.SekiroSaveFormatOffsets.settingsChecksum);  // overwrite old hash with new one

            if (!string.IsNullOrEmpty(dstPath))
                File.Copy(dstPath, Path.Combine(Path.GetDirectoryName(dstPath), Path.GetFileNameWithoutExtension(dstPath) + "_unmodified.bak"), true);
            else
                File.Copy(filePath, Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_unmodified.bak"), true);

            if (!string.IsNullOrEmpty(dstPath))
            {
                File.Delete(dstPath);
                File.WriteAllBytes(dstPath, saveData);
            }
            else
            {
                File.Delete(filePath);
                File.WriteAllBytes(filePath, saveData);
            }

            saveData = null;
            return true;
        }

        /// <summary>
        /// Backup a savegame file.
        /// </summary>
        private void BackupSaveGameFile()
        {
            if (_filePath == null || !File.Exists(_filePath))
                return;
            DateTime now = DateTime.Now;
            string backupPath = Path.Combine(Path.GetDirectoryName(_filePath), Path.GetFileNameWithoutExtension(_filePath)+ "_backup_" + now.ToString("yyyy-MM-dd-HHmmss") + ".bak");
            if (File.Exists(backupPath))
                return;
            File.Copy(_filePath, backupPath, true);
            if (File.Exists(backupPath))
            {
                this.tbSaveStatus.Text = now.ToString("HH:mm:ss") + " BACKUP SAVED";
                this.cbBackups.Items.Add(new KeyValuePair<string, string>(backupPath, now.ToString("MM/dd HH:mm:ss") + (this.tbBackupName.Text != "" ? " (" + this.tbBackupName.Text + ")" : "")));
                _settingsService.ApplicationSettings.names.Add(backupPath, this.tbBackupName.Text);
                this.tbBackupName.Text = "";
                this.cbBackups.SelectedIndex = this.cbBackups.Items.Count - 1;
            }
        }

        /// <summary>
        /// Give a backup a name.
        /// </summary>
        private void NameSaveGameFile()
        {
            if (this.cbBackups.SelectedIndex < 0)
                return;
            KeyValuePair<string, string> backup = (KeyValuePair<string, string>)this.cbBackups.SelectedItem;
            if (!_settingsService.ApplicationSettings.names.ContainsKey(backup.Key))
                _settingsService.ApplicationSettings.names.Add(backup.Key, "");
            _settingsService.ApplicationSettings.names[backup.Key] = this.tbBackupName.Text;
            int selectedIndex = this.cbBackups.SelectedIndex;
            string cleanName = backup.Value.Substring(0, backup.Value.IndexOf('(') > -1 ? backup.Value.IndexOf('(') - 1 : backup.Value.Length);
            this.cbBackups.Items.RemoveAt(selectedIndex);
            this.cbBackups.Items.Insert(selectedIndex, new KeyValuePair<string, string>(backup.Key, cleanName + (this.tbBackupName.Text != "" ? " (" + this.tbBackupName.Text + ")" : "")));
            this.cbBackups.SelectedIndex = selectedIndex;
            this.tbBackupName.Text = "";
        }

        /// <summary>
        /// Revert to a previously backup'd savegame file.
        /// </summary>
        private void RevertSaveGameFile()
        {
            if (_filePath == null || this.cbBackups.SelectedIndex < 0)
                return;
            KeyValuePair<string, string> backup = (KeyValuePair<string, string>)this.cbBackups.SelectedItem;
            if (!File.Exists(backup.Key))
                return;
            if (File.Exists(_filePath))
                File.Delete(_filePath);
            File.Copy(backup.Key, _filePath, true);
            if (File.Exists(_filePath))
                this.tbSaveStatus.Text = DateTime.Now.ToString("HH:mm:ss") + " REVERTED TO BACKUP: " + backup.Value;
        }

        /// <summary>
        /// Delete the selected savegame file.
        /// </summary>
        private void DeleteSaveGameFile()
        {
            if (this.cbBackups.SelectedIndex < 0)
                return;
            KeyValuePair<string, string> backup = (KeyValuePair<string, string>)this.cbBackups.SelectedItem;
            if (File.Exists(backup.Key))
                File.Delete(backup.Key);
            if (File.Exists(backup.Key)) return;
            this.cbBackups.Items.RemoveAt(this.cbBackups.SelectedIndex);
            if (_settingsService.ApplicationSettings.names.ContainsKey(backup.Key))
                _settingsService.ApplicationSettings.names.Remove(backup.Key);
            this.tbSaveStatus.Text = DateTime.Now.ToString("HH:mm:ss") + " BACKUP DELETED";
        }

        /// <summary>
        /// Opens file dialog.
        /// </summary>
        /// <param name="title">The title to sho in the file selection window.</param>
        /// <param name="defaultDir">The default directory to start up to.</param>
        /// <param name="defaultExt">A list of default extensions in ".extension" format.</param>
        /// <param name="filter">A list of names of a file with this extension ("Extension File").</param>
        /// <returns>The path to the selected file.</returns>
        private static string OpenFile(string title, string defaultDir, string[] defaultExt, string[] filter, bool explicitFilter = false)
        {
            if (defaultExt.Length != filter.Length)
                throw new ArgumentOutOfRangeException("defaultExt must be the same length as filter!");
            string fullFilter = "";
            if (explicitFilter)
            {
                fullFilter = filter[0] + "|" + defaultExt[0];
            }
            else
            {
                for (int i = 0; i < defaultExt.Length; i++)
                {
                    if (i > 0)
                        fullFilter += "|";
                    fullFilter += filter[i] + " (*" + defaultExt[i] + ")|*" + defaultExt[i];
                }
            }

            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = title,
                InitialDirectory = defaultDir,
                //DefaultExt = defaultExt,
                Filter = fullFilter,
                FilterIndex = 0,
            };
            bool? result = dlg.ShowDialog();
            if (result != true)
                return null;
            return File.Exists(dlg.FileName) ? dlg.FileName : null;
        }

        /// <summary>
        /// Calculate the MD5 checksum of a byte array.
        /// </summary>
        /// <param name="cbData">The array.</param>
        /// <param name="offset">The offset inside the array.</param>
        /// <param name="length">The length of the section.</param>
        /// <returns></returns>
        private byte[] CalculateMd5Checksum(in byte[] cbData, int offset = 0, int length = 0)
        {
            if (offset == 0 && length == 0)
                return _md5Provider.ComputeHash(cbData);
            if (offset >= 0 && length >= 0 && length < cbData.Length - offset)
                return _md5Provider.ComputeHash(CreateArrayFromArraySelection(cbData, offset, length));

            return null;
        }

        /// <summary>
        /// Checks if an address is valid.
        /// </summary>
        /// <param name="address">The address (the pointer points to).</param>
        /// <returns>True if (pointer points to a) valid address.</returns>
        private static bool IsValidAddress(Int64 address)
        {
            return (address >= 0x10000 && address < 0x000F000000000000);
        }

        /// <summary>
        /// Writes a given type and value to processes memory using a generic method.
        /// </summary>
        /// <param name="hProcess">The process handle to read from.</param>
        /// <param name="lpBaseAddress">The address to write from.</param>
        /// <param name="bytes">The byte array to write.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private static bool WriteBytes(IntPtr hProcess, Int64 lpBaseAddress, byte[] bytes)
        {
            return WriteProcessMemory(hProcess, lpBaseAddress, bytes, (ulong)bytes.Length, out _);
        }

        /// <summary>
        /// Creates a new byte array from a section of another array.
        /// </summary>
        /// <param name="cbData">The array to cut from.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length of the new array.</param>
        /// <returns></returns>
        private static byte[] CreateArrayFromArraySelection(in byte[] cbData, int offset = 0, int length = 0)
        {
            if (offset + (length - 1) > cbData.Length || offset < 0 || length < 0)
                return null;
            byte[] section;
            section = length < 1 ? new byte[cbData.Length - offset] : new byte[length];
            Buffer.BlockCopy(cbData, offset, section, 0, length < 1 ? cbData.Length : length);
            return section;
        }

        /// <summary>
        /// Compares two byte arrays, optionally from custom offsets.
        /// </summary>
        /// <param name="cb1">The first byte array.</param>
        /// <param name="cb2">The second byte array.</param>
        /// <param name="length">The amount of bytes to compare.</param>
        /// <param name="offset1">The index to start comparing in cb1.</param>
        /// <param name="offset2">The index to start comparing in cb2.</param>
        /// <returns></returns>
        private static bool ArrayEquals(in byte[] cb1, in byte[] cb2, int length, int offset1 = 0, int offset2 = 0)
        {
            if (offset1 + (length - 1) > cb1.Length || offset2 + (length - 1) > cb2.Length)
                return false;
            for (int i = 0; i < length; i++)
            {
                if (cb1[offset1 + i] != cb2[offset2 + i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Copies bytes from one array to another, optionally from custom offsets.
        /// </summary>
        /// <param name="cbSrc">The source array.</param>
        /// <param name="cbDst">The destination array that will be copied to.</param>
        /// <param name="length">The amount of bytes to copy.</param>
        /// <param name="offsetSrc">The index to start coping from in cbSrc.</param>
        /// <param name="offsetDst">The index to start coping to in cbDst.</param>
        private static void CopyArrayToArray(ref byte[] cbSrc, ref byte[] cbDst, int length, int offsetSrc = 0, int offsetDst = 0)
        {
            if (offsetSrc + (length - 1) > cbSrc.Length || offsetDst + (length - 1) > cbDst.Length)
                return;
            for (int i = 0; i < length; i++)
                cbDst[offsetDst + i] = cbSrc[offsetSrc + i];
        }

        /// <summary>
        /// Check whether input is alpha numeric only.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if input is alpha numeric only.</returns>
        private static bool IsAlphaNumericInput(string text)
        {
            return Regex.IsMatch(text, "[^A-Za-z0-9_\\-\\s]+");
        }

        private void TbFile_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenDefaultSaveFile();
        }

        private void CbPatchGame_Changed(object sender, RoutedEventArgs e)
        {
            if (this.cbPatchGame.IsEnabled)
                PatchSaveFileCheck();
        }

        private void CbHotkeysChanged(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (this.cbHotkeys.IsChecked == true)
            {
                if (!RegisterHotKey(hwnd, 9001, MOD_CONTROL, VK_B) ||
                    !RegisterHotKey(hwnd, 9002, MOD_CONTROL, VK_N) ||
                    !RegisterHotKey(hwnd, 9003, MOD_CONTROL, VK_R) ||
                    !RegisterHotKey(hwnd, 9004, MOD_CONTROL, VK_D))
                    MessageBox.Show("A hotkey is already in use, it may not work.", "Simple Sekiro Savegame Helper");
            }
            else
            {
                UnregisterHotKey(hwnd, 9001);
                UnregisterHotKey(hwnd, 9002);
                UnregisterHotKey(hwnd, 9003);
                UnregisterHotKey(hwnd, 9004);
            }
        }

        private void BImport_Click(object sender, RoutedEventArgs e)
        {
            ImportSaveGameFile();
        }

        private void BBackup_Click(object sender, RoutedEventArgs e)
        {
            BackupSaveGameFile();
            this.cbBackups.Focus();
        }

        private void AlphaNumeric_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = IsAlphaNumericInput(e.Text);
        }

        private void AlphaNumeric_PastingHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (IsAlphaNumericInput(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void BName_Click(object sender, RoutedEventArgs e)
        {
            NameSaveGameFile();
            this.cbBackups.Focus();
        }

        private void BRevert_Click(object sender, RoutedEventArgs e)
        {
            RevertSaveGameFile();
            this.cbBackups.Focus();
        }

        private void BDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteSaveGameFile();
            this.cbBackups.Focus();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        #region WINAPI

        private const int WM_HOTKEY_MSG_ID = 0x0312;
        private const int MOD_CONTROL = 0x0002;
        private const uint VK_B = 0x0042;
        private const uint VK_N = 0x004E;
        private const uint VK_R = 0x0052;
        private const uint VK_D = 0x0044;
        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;

        [DllImport("user32.dll")]
        public static extern Boolean RegisterHotKey(IntPtr hWnd, Int32 id, UInt32 fsModifiers, UInt32 vlc);

        [DllImport("user32.dll")]
        public static extern Boolean UnregisterHotKey(IntPtr hWnd, Int32 id);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            UInt32 dwDesiredAccess,
            Boolean bInheritHandle,
            UInt32 dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [In, Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesWritten);

        #endregion
    }
}
