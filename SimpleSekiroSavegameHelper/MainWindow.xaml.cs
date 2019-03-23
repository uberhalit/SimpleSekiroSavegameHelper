using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SimpleSekiroSavegameHelper
{
    public partial class MainWindow : Window
    {
        private string _filePath = null;
        private IntPtr _fileHandle = IntPtr.Zero;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On window loaded.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (!RegisterHotKey(hwnd, 9001, MOD_CONTROL, VK_L))
                MessageBox.Show("A hotkey is already in use, it may not work.", "Simple Sekiro Savegame Helper");
            if (!RegisterHotKey(hwnd, 9002, MOD_CONTROL, VK_U))
                MessageBox.Show("A hotkey is already in use, it may not work.", "Simple Sekiro Savegame Helper");
            if (!RegisterHotKey(hwnd, 9003, MOD_CONTROL, VK_B))
                MessageBox.Show("A hotkey is already in use, it may not work.", "Simple Sekiro Savegame Helper");
            if (!RegisterHotKey(hwnd, 9004, MOD_CONTROL, VK_R))
                MessageBox.Show("A hotkey is already in use, it may not work.", "Simple Sekiro Savegame Helper");

            // add a hook for WindowsMessageQueue to recognize hotkey-press
            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _filePath = GetLatestSaveGame();
            if (_filePath != null)
            {
                this.tbFile.Text = _filePath;
                this.tbFile.Focus();
                this.tbFile.Select(tbFile.Text.Length, 0);
                this.tbLockStatus.Text = "UNLOCKED";
                Dictionary<string, string> backups = GetBackupsToSaveGame(_filePath);
                this.cbBackups.Items.Clear();
                foreach (var backup in backups)
                    this.cbBackups.Items.Add(backup);
                this.cbBackups.SelectedIndex = this.cbBackups.Items.Count - 1;
            }
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
            if (_fileHandle != IntPtr.Zero)
                UnlockFile(_fileHandle);
        }

        /// <summary>
        /// Windows Message queue (Wndproc) to catch HotKeyPressed
        /// </summary>
        private void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (!handled)
            {
                if (msg.message == WM_HOTKEY_MSG_ID)    // hotkeyevent
                {
                    if (msg.wParam.ToInt32() == 9001)   // lock file
                    {
                        handled = true;
                        LockSaveGameFile();
                    }
                    else if (msg.wParam.ToInt32() == 9002)  // unlock file
                    {
                        handled = true;
                        UnlockSaveGameFile();
                    }
                    else if (msg.wParam.ToInt32() == 9003)  // backup savegame
                    {
                        handled = true;
                        BackupSaveGameFile();
                    }
                    else if (msg.wParam.ToInt32() == 9004)  // load savegame
                    {
                        handled = true;
                        RevertSaveGameFile();
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
        /// Lock savegame file and update status.
        /// </summary>
        private void LockSaveGameFile()
        {
            if (_fileHandle != IntPtr.Zero || _filePath == null)
                return;
            _fileHandle = LockFile(_filePath);
            if (_fileHandle != IntPtr.Zero)
            {
                this.tbLockStatus.Background = Brushes.Green;
                this.tbLockStatus.Text = "LOCKED";
            }   
        }

        /// <summary>
        /// Unlock savegame and update status.
        /// </summary>
        private bool UnlockSaveGameFile()
        {
            if (_fileHandle == IntPtr.Zero)
                return false;
            if (UnlockFile(_fileHandle))
            {
                this._fileHandle = IntPtr.Zero;
                this.tbLockStatus.Background = Brushes.White;
                this.tbLockStatus.Text = "UNLOCKED";
            }
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
                this.tbSaveStatus.Text = "BACKUP SAVED: " + now.ToString("HH:mm:ss");
                this.cbBackups.Items.Add(new KeyValuePair<string, string>(backupPath, now.ToString("MM/dd HH:mm:ss")));
                this.cbBackups.SelectedIndex = this.cbBackups.Items.Count - 1;
            }
        }

        /// <summary>
        /// Revert to a previously backup'd savegame file.
        /// </summary>
        private void RevertSaveGameFile()
        {
            if (_filePath == null || !File.Exists(_filePath) || this.cbBackups.SelectedIndex < 0)
                return;
            KeyValuePair<string, string> backup = (KeyValuePair<string, string>)this.cbBackups.SelectedItem;
            if (!File.Exists(backup.Key))
                return;
            if (_fileHandle != IntPtr.Zero)
            {
                if (!UnlockSaveGameFile())
                {
                    MessageBox.Show("Could not unlock file!", "Simple Sekiro Savegame Helper");
                    return;
                }
            }
            File.Delete(_filePath);
            File.Copy(backup.Key, _filePath, true);
            if (File.Exists(_filePath))
            {
                this.tbSaveStatus.Text = "REVERTED TO BACKUP FROM: " + backup.Value;
            }
        }

        /// <summary>
        /// Locks a file to protect it from any writing access. File handle must be kept open, calling process must be of equal or higher privileges than accessing process.
        /// </summary>
        /// <param name="filename">The full path to the file.</param>
        /// <returns>The handle to the locked file if LockFile was successfully, IntPtr.Zero otherwise.</returns>
        /// <remarks>If the returned filehandle gets closed the lock will open up so leave the calling application running</remarks>
        private static IntPtr LockFile(string filename)
        {
            IntPtr hFile = CreateFile(filename, GENERIC_READ, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (hFile == IntPtr.Zero)
                return IntPtr.Zero;
            uint dwHigh = 0;
            uint dwLow = GetFileSize(hFile, dwHigh);
            if (dwLow < 10)
            {
                CloseHandle(hFile);
                return IntPtr.Zero;
            }
            System.Threading.NativeOverlapped ov = new System.Threading.NativeOverlapped();
            if (!LockFileEx(hFile, LOCKFILE_EXCLUSIVE_LOCK, 0, dwLow, dwHigh, ref ov))
            {
                CloseHandle(hFile);
                return IntPtr.Zero;
            }
            return hFile;
        }

        /// <summary>
        /// Unlocks a previous locked file.
        /// </summary>
        /// <param name="hFile">The handle to the locked file.</param>
        /// <returns>True if the file got unlocked successfully, false otherwise.</returns>
        /// <remarks>This will further close the handle to the file.</remarks>
        private static bool UnlockFile(IntPtr hFile)
        {
            if (hFile == IntPtr.Zero)
                return false;
            uint dwHigh = 0;
            uint dwLow = GetFileSize(hFile, dwHigh);
            if (dwLow < 10)
            {
                CloseHandle(hFile);
                return false;
            }
            System.Threading.NativeOverlapped ov = new System.Threading.NativeOverlapped();
            if (!UnlockFileEx(hFile, 0, dwLow, dwHigh, ref ov))
                return false;
            CloseHandle(hFile);
            return true;
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

        private void TbFile_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string newPath = OpenFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sekiro"), ".sl2", "Sekiro Save Game File");
            if (!string.IsNullOrEmpty(newPath))
            {
                if (_fileHandle != IntPtr.Zero)
                {
                    if (!UnlockSaveGameFile())
                    {
                        MessageBox.Show("Could not unlock previous file!", "Simple Sekiro Savegame Helper");
                    }
                }
                _filePath = newPath;
                this.tbFile.Text = _filePath;
                this.tbFile.Focus();
                this.tbFile.Select(tbFile.Text.Length, 0);
                this.tbLockStatus.Text = "UNLOCKED";
                Dictionary<string, string> backups = GetBackupsToSaveGame(_filePath);
                this.cbBackups.Items.Clear();
                foreach (var backup in backups)
                    this.cbBackups.Items.Add(backup);
                this.cbBackups.SelectedIndex = this.cbBackups.Items.Count - 1;
            }
        }

        private void BLock_Click(object sender, RoutedEventArgs e)
        {
            LockSaveGameFile();
        }

        private void BUnlock_Click(object sender, RoutedEventArgs e)
        {
            UnlockSaveGameFile();
        }

        private void BBackup_Click(object sender, RoutedEventArgs e)
        {
            BackupSaveGameFile();
        }

        private void BRevert_Click(object sender, RoutedEventArgs e)
        {
            RevertSaveGameFile();
        }

        #region WINAPI

        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint OPEN_EXISTING = 3;
        private const uint LOCKFILE_EXCLUSIVE_LOCK = 0x00000002;
        private const int WM_HOTKEY_MSG_ID = 0x0312;
        private const int MOD_CONTROL = 0x0002;
        private const uint VK_L = 0x004C;
        private const uint VK_U = 0x0055;
        private const uint VK_B = 0x0042;
        private const uint VK_R = 0x0052;

        [DllImport("user32.dll")]
        public static extern Boolean RegisterHotKey(IntPtr hWnd, Int32 id, UInt32 fsModifiers, UInt32 vlc);

        [DllImport("user32.dll")]
        public static extern Boolean UnregisterHotKey(IntPtr hWnd, Int32 id);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(
            String lpFileName,
            UInt32 dwDesiredAccess,
            UInt32 dwShareMode,
            IntPtr lpSecurityAttributes,
            UInt32 dwCreationDisposition,
            UInt32 dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll")]
        private static extern UInt32 GetFileSize(IntPtr hFile, UInt32 lpFileSizeHigh);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean LockFileEx(
            IntPtr hFile,
            UInt32 dwFlags,
            UInt32 dwReserved,
            UInt32 nNumberOfBytesToLockLow,
            UInt32 nNumberOfBytesToLockHigh,
            [In] ref System.Threading.NativeOverlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean UnlockFileEx(
            IntPtr hFile,
            UInt32 dwReserved,
            UInt32 nNumberOfBytesToUnlockLow,
            UInt32 nNumberOfBytesToUnlockHigh,
            [In] ref System.Threading.NativeOverlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean CloseHandle(IntPtr hObject);

        #endregion
    }
}
