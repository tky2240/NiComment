using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.ComponentModel;
using Reactive.Bindings;
using System.Net.WebSockets;
using System.Threading;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Controls;
using NiComment.Properties;
using NiComment.Keycloak;

namespace NiComment
{
    internal class SharedViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly SharedModel _SharedModel;
        private int i = 0;
        private readonly int _CommentSlots = 20;
        private readonly int _FixedCommentSlots = 10;
        private readonly int _CommentLayers = 2;
        private readonly int _AnimationDurationSecond = 4;
        private readonly int _AnimationDurationMinute = 5;
        private KeycloakClient _KeycloakClient = null;
        private Canvas _CommentCanvas = null;
        private Window _CommentWindow = null;
        public ReactiveCollection<Comment> Comments { get; } = new ReactiveCollection<Comment>();
        //public ReactiveCollection<CommentSetting> CommentSettings { get; } = new ReactiveCollection<CommentSetting>();
        public ConcurrentQueue<CommentSetting> CommentSettingsQueue = new ConcurrentQueue<CommentSetting>();
        public PriorityQueue<int, int> UsedLayersQueue = new PriorityQueue<int, int>();
        public ConcurrentQueue<CommentSetting> FixedCommentSettingsQueue = new ConcurrentQueue<CommentSetting>();
        public PriorityQueue<int, int> FixedUsedLayersQueue = new PriorityQueue<int, int>();
        public SynchronizedCollection<bool> IsUsedLayers = new SynchronizedCollection<bool>();
        public ReactiveCommand ShowCommentWindowCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ChangeFullScreenCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ChangeTransparencyCommand { get; } = new ReactiveCommand();
        public ReactiveCommand<Comment> BanUserCommand { get; } = new ReactiveCommand<Comment>();
        public ReactiveCommand AddCommentCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AddManyCommentsCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ShowSettingWindowCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ClosedCommand { get; } = new ReactiveCommand();

        public SharedViewModel()
        {
            ShowCommentWindowCommand.Subscribe(() => ShowCommentWindow());
            AddCommentCommand.Subscribe(() => ShowComment());
            AddManyCommentsCommand.Subscribe(() => AddManyComments());
            ChangeFullScreenCommand.Subscribe(() => ChangeFullScreen());
            ChangeTransparencyCommand.Subscribe(() => ChangeTransparency());
            _SharedModel = new SharedModel();
            UsedLayersQueue.EnsureCapacity(_CommentSlots * _CommentLayers - 1);
            FixedUsedLayersQueue.EnsureCapacity(_FixedCommentSlots * _CommentLayers - 1);
            Enumerable.Range(0, _CommentSlots * _CommentLayers - 1).ToList().ForEach(x => UsedLayersQueue.Enqueue(x, x));
            Enumerable.Range(0, _FixedCommentSlots * _CommentLayers - 1).ToList().ForEach(x => FixedUsedLayersQueue.Enqueue(x, x));
            Enumerable.Repeat(false, _CommentSlots * _CommentLayers - 1).ToList().ForEach(x => IsUsedLayers.Add(x));
            ShowSettingWindowCommand.Subscribe(() => ShowSettingWindow());
            BanUserCommand.Subscribe((comment) => BanUser(comment));
            ClosedCommand.Subscribe(() => ClosedWindow());
        }

        private void ShowSettingWindow() {
            SettingWindow settingWindow = new SettingWindow();
            settingWindow.Show();
        }

        private void ChangeFullScreen() {
            if(_CommentWindow.WindowState == WindowState.Normal) {
                _CommentWindow.WindowState = WindowState.Maximized;
            }else if (_CommentWindow.WindowState == WindowState.Maximized) {
                _CommentWindow.WindowState = WindowState.Normal; 
            }
        }

        private void ChangeTransparency() {
            Brush background = _CommentWindow.Background;
            if(background.Opacity == 0) {
                background.Opacity = 0.8;
            } else {
                background.Opacity = 0;
            }
        }

        private async void BanUser(Comment comment) {
            if (MessageBox.Show($"ユーザー:{comment.UserName}をブロックしますか？", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes ){
                if(await _KeycloakClient.BanUser(Settings.Default.Password, comment.UserID)){
                    MessageBox.Show("成功しました");
                } else {
                    MessageBox.Show("失敗しました");
                }
            }
            
        }

        private async void OnRecordProgressChanged(Comment comment)
        {
            AddComment(comment);
        }

        private void ShowCommentWindow() {
            if (_CommentWindow != null) {
                MessageBox.Show("コメントウィンドウの複数起動はできません");
                return;
            }
            _KeycloakClient = new KeycloakClient(Settings.Default.AdminCliSecret, "admin-cli", Settings.Default.UserName);
            _SharedModel.ConnectWebSocket(new Progress<Comment>(OnRecordProgressChanged));
            CommentWindow commentWindow = new CommentWindow();
            commentWindow.Show();
            Application.Current.MainWindow.Owner = commentWindow;
            _CommentCanvas = commentWindow.MainCanvas;
            _CommentWindow = commentWindow;
        }

        private void ShowComment() {
            i += 1;
            Comment comment = new Comment() { Message = i.ToString(), UserName = "test" };
            _SharedModel.SendMessage(comment);
            //AddComment(record);
        }

        private async void AddManyComments() {
            for (int j = 0; j < 50; j++){
                await Task.Delay(25);
                AddComment(new Comment() { Message = j.ToString(), UserName ="test", IsFixedComment = false});
                AddComment(new Comment() { Message = "インテル長友", UserName = "test", IsFixedComment = true});
            }
        }

        private void AddComment(Comment comment)
        {
            Comments.AddOnScheduler(comment);
            if (comment.IsFixedComment) {
                while (true) {
                    if (FixedUsedLayersQueue.TryDequeue(out var emptyLayerIndex, out var priority)) {
                        Storyboard storyboard = new Storyboard();
                        BooleanAnimationUsingKeyFrames booleanAnimationUsingKeyFrames = new BooleanAnimationUsingKeyFrames() {
                            Duration = new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)),
                        };
                        //DoubleAnimation doubleAnimation = new DoubleAnimation(_CommentWindow.ActualWidth, -30 * record.message.Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                        Storyboard.SetTargetProperty(booleanAnimationUsingKeyFrames, new PropertyPath(TextBlock.IsEnabledProperty));
                        storyboard.Children.Add(booleanAnimationUsingKeyFrames);
                        ClockGroup clockGroup = storyboard.CreateClock();
                        clockGroup.Completed += FixedCommentClockGroup_Completed;
                        TextBlock textBlock = new TextBlock() {
                            Text = comment.Message,
                            FontSize = _CommentWindow.ActualHeight / _FixedCommentSlots,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                        };
                        Canvas.SetTop(textBlock, _CommentWindow.ActualHeight / _FixedCommentSlots * (emptyLayerIndex % _FixedCommentSlots) + (int)(emptyLayerIndex / _FixedCommentSlots) * _CommentWindow.ActualHeight / _FixedCommentSlots / 2);
                        Canvas.SetLeft(textBlock, (_CommentWindow.ActualWidth - _CommentWindow.ActualHeight / _FixedCommentSlots * comment.Message.Length) /2);
                        _CommentCanvas.Children.Add(textBlock);
                        storyboard.Begin(textBlock, true);
                        CommentSetting commentSetting = new CommentSetting() {
                            Message = comment.Message,
                            UserName = comment.UserName,
                            UsdeLayerIndex = emptyLayerIndex,
                            Storyboard = storyboard,
                            ClockGroup = clockGroup,
                            TextBlock = textBlock,
                        };
                        FixedCommentSettingsQueue.Enqueue(commentSetting);
                        break;
                    } else {
                        if (FixedCommentSettingsQueue.TryDequeue(out var oldestCommentSetting)) {
                            oldestCommentSetting.ClockGroup.Completed -= FixedCommentClockGroup_Completed;
                            oldestCommentSetting.Storyboard.Remove();
                            oldestCommentSetting.TextBlock.Visibility = Visibility.Collapsed;
                            _CommentCanvas.Children.Remove(oldestCommentSetting.TextBlock);
                            FixedUsedLayersQueue.Enqueue(oldestCommentSetting.UsdeLayerIndex, oldestCommentSetting.UsdeLayerIndex);
                        }
                    }
                }
            } else {
                while (true) {
                    if (UsedLayersQueue.TryDequeue(out var emptyLayerIndex, out var priority)) {
                        Storyboard storyboard = new Storyboard();
                        DoubleAnimation doubleAnimation = new DoubleAnimation(_CommentWindow.ActualWidth, -30 * comment.Message.Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                        Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(Canvas.LeftProperty));
                        storyboard.Children.Add(doubleAnimation);
                        ClockGroup clockGroup = storyboard.CreateClock();
                        clockGroup.Completed += CommentClockGroup_Completed;
                        TextBlock textBlock = new TextBlock() {
                            Text = comment.Message,
                            FontSize = _CommentWindow.ActualHeight / _CommentSlots,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
                        };
                        Canvas.SetTop(textBlock, _CommentWindow.ActualHeight / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentSlots) * _CommentWindow.ActualHeight / _CommentSlots / 2);
                        _CommentCanvas.Children.Add(textBlock);
                        storyboard.Begin(textBlock, true);
                        CommentSetting commentSetting = new CommentSetting() {
                            Message = comment.Message,
                            UserName = comment.UserName,
                            UsdeLayerIndex = emptyLayerIndex,
                            Storyboard = storyboard,
                            ClockGroup = clockGroup,
                            TextBlock = textBlock,
                        };
                        CommentSettingsQueue.Enqueue(commentSetting);
                        break;
                    } else {
                        if (CommentSettingsQueue.TryDequeue(out var oldestCommentSetting)) {
                            oldestCommentSetting.ClockGroup.Completed -= CommentClockGroup_Completed;
                            oldestCommentSetting.Storyboard.Remove();
                            oldestCommentSetting.TextBlock.Visibility = Visibility.Collapsed;
                            _CommentCanvas.Children.Remove(oldestCommentSetting.TextBlock);
                            UsedLayersQueue.Enqueue(oldestCommentSetting.UsdeLayerIndex, oldestCommentSetting.UsdeLayerIndex);
                        }
                    }
                }
            }
        }

        private void CommentClockGroup_Completed(object? sender, EventArgs e) {
            while(true) {
                if (CommentSettingsQueue.TryDequeue(out var oldestCommentSetting)) {
                    UsedLayersQueue.Enqueue(oldestCommentSetting.UsdeLayerIndex, oldestCommentSetting.UsdeLayerIndex);
                    _CommentCanvas.Children.Remove(oldestCommentSetting.TextBlock);
                    break;
                }else if (!CommentSettingsQueue.Any()) {
                    break;
                }
            }
        }
        private void FixedCommentClockGroup_Completed(object? sender, EventArgs e) {
            while (true) {
                if (FixedCommentSettingsQueue.TryDequeue(out var oldestCommentSetting)) {
                    FixedUsedLayersQueue.Enqueue(oldestCommentSetting.UsdeLayerIndex, oldestCommentSetting.UsdeLayerIndex);
                    _CommentCanvas.Children.Remove(oldestCommentSetting.TextBlock);
                    break;
                } else if (!FixedCommentSettingsQueue.Any()) {
                    break;
                }
            }
        }

        private void Storyboard_Completed(object? sender, EventArgs e) {
            if (!CommentSettingsQueue.TryDequeue(out var oldestCommentSetting)) {
                return;
            }
            IsUsedLayers[oldestCommentSetting.UsdeLayerIndex] = false;
        }

        private void ClosedWindow() {
            if(_CommentWindow != null) {
                _CommentWindow.Close();
            }
        }
    }

    public record class Comment
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }
        public bool IsFixedComment { get; set; }
    }

    public record class CommentSetting {
        public string Message { get; set; }
        public string UserName { get; set; }
        public int UsdeLayerIndex { get; set; }
        public Storyboard Storyboard { get; set; }
        public ClockGroup ClockGroup { get; set; }
        public TextBlock TextBlock { get; set; }
    }
}
