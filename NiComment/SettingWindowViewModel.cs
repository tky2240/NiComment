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
        // public ReactiveProperty<string> Password { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> WebSocketHost { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> WebSocketPort { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> WebSocketPath { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> KeycloakHost { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> KeycloakPort { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> MasterRealm { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> NiCommentRealm { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> AdminCliSecret { get; } = new ReactiveProperty<string>();
        public ReactiveCommand<PasswordBox> LoadCurrentSettingsCommand { get; } = new ReactiveCommand<PasswordBox>();
        public ReactiveCommand<SettingWindow> RegisterSettingsCommand { get; } = new ReactiveCommand<SettingWindow>();
        public ReactiveCommand<Window> CloseWindowCommand { get; } = new ReactiveCommand<Window>();
        //private readonly SettingWindowModel _SettingWindowModel = new SettingWindowModel();

        public SettingWindowViewModel() {
            LoadCurrentSettingsCommand.Subscribe((passwordBox) => LoadCurrentSettings(passwordBox));
            RegisterSettingsCommand.Subscribe((window) => RegisterSettings(window));
            CloseWindowCommand.Subscribe((window) => CloseWindow(window));
        }

        private void LoadCurrentSettings(PasswordBox passwordBox) {
            Settings currentSettings = Settings.Default;
            KeycloakHost.Value = currentSettings.KeycloakHost;
            KeycloakPort.Value = currentSettings.KeycloakPort.ToString();
            UserName.Value = currentSettings.UserName;
            passwordBox.Password = currentSettings.Password;
            WebSocketHost.Value = currentSettings.WebSocketHost;
            WebSocketPath.Value = currentSettings.WebSocketPath;
            WebSocketPort.Value = currentSettings.WebSocketPort.ToString();
            MasterRealm.Value = currentSettings.MasterRealm;
            NiCommentRealm.Value = currentSettings.NiCommentRealm;
            AdminCliSecret.Value = currentSettings.AdminCliSecret;
        }
        private void RegisterSettings(SettingWindow window) {
            (bool IsEmpty, string Message)[] checkEmpties = {
                (string.IsNullOrWhiteSpace(KeycloakHost.Value?.Trim()), "Keycloakホストが空欄です"),
                (!ushort.TryParse(KeycloakPort.Value, out ushort keycloakPort), "Keycloakポートが不正です"),
                (string.IsNullOrWhiteSpace(UserName.Value?.Trim()), "ユーザー名が空欄です"),
                (string.IsNullOrWhiteSpace(window.PasswordBox.Password), "パスワードが空欄です"),
                (string.IsNullOrWhiteSpace(MasterRealm.Value?.Trim()), "管理用Realmが空欄です"),
                (string.IsNullOrWhiteSpace(NiCommentRealm.Value?.Trim()), "投稿用Realmが空欄です"),
                (string.IsNullOrWhiteSpace(WebSocketHost.Value?.Trim()), "WebSocketホストが空欄です"),
                (string.IsNullOrWhiteSpace(WebSocketPath.Value?.Trim()), "WebSocketパスが空欄です"),
                (!ushort.TryParse(WebSocketPort.Value, out ushort webSocketPort), "WebSocketポートが不正です"),
                (string.IsNullOrWhiteSpace(AdminCliSecret.Value?.Trim()), "AdminCliSecretが空欄です"),
            };
            foreach((bool IsEmpty, string Message) checkEmpty in checkEmpties) {
                if (checkEmpty.IsEmpty) {
                    MessageBox.Show(checkEmpty.Message);
                    return;
                }
            }
            Settings currentSettings = Settings.Default;
            currentSettings.KeycloakHost = KeycloakHost.Value.Trim();
            currentSettings.KeycloakPort = keycloakPort;
            currentSettings.UserName = UserName.Value.Trim();
            currentSettings.Password = window.PasswordBox.Password;
            currentSettings.WebSocketHost = WebSocketHost.Value.Trim();
            currentSettings.WebSocketPath = WebSocketPath.Value.Trim();
            currentSettings.WebSocketPort = webSocketPort;
            currentSettings.MasterRealm = MasterRealm.Value.Trim();
            currentSettings.NiCommentRealm = NiCommentRealm.Value.Trim();
            currentSettings.AdminCliSecret = AdminCliSecret.Value.Trim();
            currentSettings.Save();
            MessageBox.Show("完了しました");
            window.Close();
        }
        private void CloseWindow(Window window) {
            window.Close();
        }
    }
}
