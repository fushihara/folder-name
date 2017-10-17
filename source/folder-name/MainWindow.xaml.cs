using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace folder_name {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window {
        private String parentDirectoryPath;
        private static readonly UnicodeEncoding FileEncode = new UnicodeEncoding(false, true);
        public MainWindow() {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, EventArgs e) {
            load();
        }
        private void load() {
            // 引数にパラメーターがあるかのチェック
            var cmds = Environment.GetCommandLineArgs();
            //cmds = new String[] { "", @"" };
            this.parentDirectoryTextBox.Text = "";
            this.physicalDirectoryNameTextBox.Text = "";
            this.logicalDirectoryNameTextBox.Text = "";
            {
                if (cmds.Length < 2) {
                    String help = @"
レジストリの HKEY_CLASSES_ROOT\Directory\shell\ の中にキーを登録して下さい。
詳しくはhttps://example.com へ".TrimStart();
                    showOverlay(message: $"フォルダを選択して下さい\n{help}", showProgress: false);
                    return;
                }
            }
            parentDirectoryPath = cmds[1];
            var directoryInfo = new DirectoryInfo(this.parentDirectoryPath);
            if (directoryInfo.Exists == false) {
                showOverlay(message: "フォルダがありません", showProgress: false);
                return;
            }
            //desktopIniの有無をチェック
            String desktopIniPath = getDesktopIniPath();
            if (!File.Exists(desktopIniPath)) { // desktop.iniが無い
                this.parentDirectoryTextBox.Text = directoryInfo.Parent.FullName;
                this.physicalDirectoryNameTextBox.Text = directoryInfo.Name;
                this.physicalDirectoryNameTextBox.SelectAll();
                this.physicalDirectoryNameTextBox.Focus();
                this.logicalDirectoryNameTextBox.Text = "";
                this.applyButton.IsEnabled = false;
                return;
            }
            // 中身を読み込む
            String localizedResourceNameRaw = this.getLocalizedResourceNameRawValue(desktopIniPath: desktopIniPath);
            // 「LocalizedResourceName=@%SystemRoot%\system32\shell32.dll,-21798」かどうか調べる
            Regex resourceDllReg = new Regex(@"^@.+?,([0-9-]+)$");
            if (resourceDllReg.IsMatch(localizedResourceNameRaw)) {
                // リソース形式なので弾く
                this.parentDirectoryTextBox.Text = directoryInfo.Parent.FullName;
                this.physicalDirectoryNameTextBox.Text = directoryInfo.Name;
                this.logicalDirectoryNameTextBox.Text = localizedResourceNameRaw;
                this.logicalDirectoryNameTextBox.IsReadOnly = true;
                this.applyButton.IsEnabled = false;
                showOverlay(message: "リソース形式には対応しておりません", showProgress: false);
                return;
            }
            this.parentDirectoryTextBox.Text = directoryInfo.Parent.FullName;
            this.physicalDirectoryNameTextBox.Text = directoryInfo.Name;
            this.logicalDirectoryNameTextBox.Text = localizedResourceNameRaw;
            this.logicalDirectoryNameTextBox.SelectAll();
            this.logicalDirectoryNameTextBox.Focus();
            this.applyButton.IsEnabled = false;
        }
        private void showOverlay(String message, bool showProgress) {
            this.overlay_message.Text = message;
            this.overlay_progress.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
            this.overlay_master.Visibility = Visibility.Visible;
        }

        private String getLocalizedResourceNameRawValue(String desktopIniPath) {
            using (StreamReader sr = new System.IO.StreamReader(desktopIniPath, MainWindow.FileEncode)) {
                Boolean checkedShellClassInfo = false;
                Regex localizedResourceNameRegex = new Regex(@"^LocalizedResourceName\s*=\s*(.+)");
                while (sr.Peek() > -1) {
                    String line = sr.ReadLine().Trim();
                    if (checkedShellClassInfo == false && line == "[.ShellClassInfo]") {
                        checkedShellClassInfo = true;
                    } else if (checkedShellClassInfo == true && localizedResourceNameRegex.IsMatch(line)) {
                        String matchText = localizedResourceNameRegex.Match(line).Groups[1].Value;
                        return matchText;
                    }
                }
            }
            return "";
        }

        private void logicalDirectoryNameTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (this.applyButton == null) { return; }
            if (sender is TextBox a) {
                if (a.Text != "") {
                    this.applyButton.IsEnabled = true;
                }
            }
        }

        private void applyButton_Click(object sender, RoutedEventArgs e) {
            String saveValue = this.logicalDirectoryNameTextBox.Text;
            showOverlay(message: "反映中です", showProgress: true);
            String desktopIniPath = getDesktopIniPath();
            if (File.Exists(desktopIniPath)) {
                this.saveDesktopInitForOverwrite(logicalDirectoryName: saveValue);
            } else {
                this.saveDesktopIniForNewFile(logicalDirectoryName: saveValue);
            }
        }
        private void saveDesktopIniForNewFile(String logicalDirectoryName) {
            String saveString = $@"
[.ShellClassInfo]
LocalizedResourceName = {logicalDirectoryName}
".TrimStart();
            String iniPath = getDesktopIniPath();
            using (StreamWriter sw = new System.IO.StreamWriter(iniPath, false, MainWindow.FileEncode)) {
                sw.Write(saveString);
            }
            applyFileAttributes();
            Application.Current.Shutdown();
        }
        private String getDesktopIniPath() {
            return System.IO.Path.Combine(this.parentDirectoryPath, "desktop.ini");
        }
        private void saveDesktopInitForOverwrite(String logicalDirectoryName) {
            List<String> saveFileContentLines = new List<string>();
            String iniPath = getDesktopIniPath();
            unsetFileAttributes();
            using (StreamReader sr = new System.IO.StreamReader(iniPath, MainWindow.FileEncode)) {
                Boolean checkedShellClassInfo = false;
                Regex localizedResourceNameRegex = new Regex(@"^LocalizedResourceName\s*=\s*(.+)");
                while (sr.Peek() > -1) {
                    String line = sr.ReadLine().Trim();
                    if (checkedShellClassInfo == false && line == "[.ShellClassInfo]") {
                        checkedShellClassInfo = true;
                        saveFileContentLines.Add(line);
                    } else if (checkedShellClassInfo == true && localizedResourceNameRegex.IsMatch(line)) {
                        saveFileContentLines.Add($"LocalizedResourceName = {logicalDirectoryName}");
                    } else {
                        saveFileContentLines.Add(line);
                    }
                }
            }
            using (StreamWriter sw = new System.IO.StreamWriter(iniPath, false, MainWindow.FileEncode)) {
                sw.Write(String.Join("\r\n", saveFileContentLines));
            }
            applyFileAttributes();
            Application.Current.Shutdown();
        }
        private void unsetFileAttributes() {
            FileAttributes attr = File.GetAttributes(getDesktopIniPath());
            attr &= (~FileAttributes.Archive);
            attr &= (~FileAttributes.System);
            attr &= (~FileAttributes.Hidden);
            File.SetAttributes(getDesktopIniPath(), attr);

            attr = File.GetAttributes(this.parentDirectoryPath);
            attr &= (~FileAttributes.System);
            File.SetAttributes(this.parentDirectoryPath, attr);
        }
        private void applyFileAttributes() {
            FileAttributes attr = File.GetAttributes(getDesktopIniPath());
            attr |= FileAttributes.Archive;
            attr |= FileAttributes.System;
            attr |= FileAttributes.Hidden;
            File.SetAttributes(getDesktopIniPath(), attr);

            attr = File.GetAttributes(this.parentDirectoryPath);
            attr |= FileAttributes.ReadOnly;
            File.SetAttributes(this.parentDirectoryPath, attr);
        }

        private void logicalDirectoryNameTextBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter && this.applyButton.IsEnabled) {
                applyButton_Click(null, null);
            } else if (e.Key == Key.Escape) {
                Application.Current.Shutdown();
            }
        }
    }
}
