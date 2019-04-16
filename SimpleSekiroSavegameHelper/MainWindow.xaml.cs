using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SimpleSekiroSavegameHelper
{
    public partial class MainWindow : Window
    {
        internal string _filePath = null;
        internal SettingsService _settingsService;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On window loaded.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _filePath = GetLatestSaveGame();
            if (_filePath == null)
                return;

            _settingsService = new SettingsService(Path.GetDirectoryName(_filePath) + @"\SimpleSekiroSavegameHelper.xml");
            _settingsService.Load();

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
        }

        /// <summary>
        /// On window closing.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcherThreadFilterMessage;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, 9001);
            UnregisterHotKey(hwnd, 9002);
            UnregisterHotKey(hwnd, 9003);
            UnregisterHotKey(hwnd, 9004);
            _settingsService.Save();
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

            string[] filePaths = Directory.GetFiles(defaultPath, "*.sl2", SearchOption.AllDirectories);
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
        /// <param name="defaultDir">The default directory to start up to.</param>
        /// <param name="defaultExt">The default extension in ".extension" format.</param>
        /// <param name="filter">The default name of a file with this extension ("Extension File").</param>
        /// <returns>The path to the selected file.</returns>
        private static string OpenFile(string defaultDir, string defaultExt, string filter)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                InitialDirectory = defaultDir,
                DefaultExt = defaultExt,
                Filter = filter + " (*" + defaultExt + ")|*" + defaultExt
            };
            bool? result = dlg.ShowDialog();
            if (result != true)
                return null;
            return File.Exists(dlg.FileName) ? dlg.FileName : null;
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
            string newPath = OpenFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sekiro"), ".sl2", "Sekiro Save Game File");
            if (!string.IsNullOrEmpty(newPath))
            {
                _filePath = newPath;
                this.tbFile.Text = _filePath;
                this.tbFile.Focus();
                this.tbFile.Select(tbFile.Text.Length, 0);
                Dictionary<string, string> backups = GetBackupsToSaveGame(_filePath);
                this.cbBackups.Items.Clear();
                foreach (var backup in backups)
                    this.cbBackups.Items.Add(backup);
                this.cbBackups.SelectedIndex = this.cbBackups.Items.Count - 1;
            }
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

        #region WINAPI

        private const int WM_HOTKEY_MSG_ID = 0x0312;
        private const int MOD_CONTROL = 0x0002;
        private const uint VK_B = 0x0042;
        private const uint VK_N = 0x004E;
        private const uint VK_R = 0x0052;
        private const uint VK_D = 0x0044;

        [DllImport("user32.dll")]
        public static extern Boolean RegisterHotKey(IntPtr hWnd, Int32 id, UInt32 fsModifiers, UInt32 vlc);

        [DllImport("user32.dll")]
        public static extern Boolean UnregisterHotKey(IntPtr hWnd, Int32 id);

        #endregion
    }
}
