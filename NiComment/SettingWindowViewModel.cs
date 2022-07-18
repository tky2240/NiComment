using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Reactive.Bindings;
using NiComment.Properties;
using System.Windows.Controls;
using System.Windows;

namespace NiComment {
    internal class SettingWindowViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;
        public ReactiveProperty<string> UserName { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> Password { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> WebSocketURI { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> WebSocketPort { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> MasterRealm { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> NiCommentRealm { get; } = new ReactiveProperty<string>();
        public ReactiveCommand<PasswordBox> LoadCurrentSettingsCommand { get; } = new ReactiveCommand<PasswordBox>();
        public ReactiveCommand<(PasswordBox, Window)> SetSettingsCommand { get; } = new ReactiveCommand<(PasswordBox, Window)>();
        public ReactiveCommand<Window> CloseWindowCommand { get; } = new ReactiveCommand<Window>();
        //private readonly SettingWindowModel _SettingWindowModel = new SettingWindowModel();

        public SettingWindowViewModel() {
            LoadCurrentSettingsCommand.Subscribe((passwordBox) => LoadCurrentSettings(passwordBox));
            SetSettingsCommand.Subscribe<(PasswordBox passwordBox, Window window)>((x) => SetSettings(x.passwordBox, x.window));
            CloseWindowCommand.Subscribe((window) => CloseWindow(window));
        }

        private void LoadCurrentSettings(PasswordBox passwordBox) {
            Settings currentSettings = Settings.Default;
            UserName.Value = currentSettings.UserName;
            passwordBox.Password = currentSettings.Password;
            WebSocketURI.Value = currentSettings.WebSocketURI;
            WebSocketPort.Value = currentSettings.WebSocketPort.ToString();
            MasterRealm.Value = currentSettings.MasterRealm;
            NiCommentRealm.Value = currentSettings.NiCommentRealm;
        }
        private void SetSettings(PasswordBox passwordBox, Window window) {
            (bool IsEmpty, string Message)[] checkEmpties = {
                (string.IsNullOrWhiteSpace(UserName.Value?.Trim()), "ユーザー名が空欄です"),
                (string.IsNullOrWhiteSpace(passwordBox.Password), "パスワードが空欄です"),
                (string.IsNullOrWhiteSpace(WebSocketURI.Value?.Trim()), "WebSocketURIが空欄です"),
                (ushort.TryParse(WebSocketPort.Value, out _), "ポートが不正です"),
                (ushort.TryParse(WebSocketPort.Value, out ushort port) ? port < 1024 : false, "Well-Knownポートは指定できません"),
                (string.IsNullOrWhiteSpace(MasterRealm.Value?.Trim()), "管理用Realmが空欄です"),
                (string.IsNullOrWhiteSpace(NiCommentRealm.Value?.Trim()), "投稿用Realmが空欄です"),
            };
            foreach((bool IsEmpty, string Message) checkEmpty in checkEmpties) {
                if (checkEmpty.IsEmpty) {
                    MessageBox.Show(checkEmpty.Message);
                    return;
                }
            }
            Settings currentSettings = Settings.Default;
            currentSettings.UserName = UserName.Value.Trim();
            currentSettings.Password = passwordBox.Password;
            currentSettings.WebSocketURI = WebSocketURI.Value.Trim();
            currentSettings.WebSocketPort = port;
            currentSettings.MasterRealm = MasterRealm.Value.Trim();
            currentSettings.NiCommentRealm = NiCommentRealm.Value.Trim();
            currentSettings.Save();
            MessageBox.Show("完了しました");
            window.Close();
        }
        private void CloseWindow(Window window) {
            window.Close();
        }
    }
}
