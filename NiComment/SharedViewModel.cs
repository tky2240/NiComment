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
        private Canvas _CommentCanvas = null;
        private Window _CommentWindow = null;
        public ReactiveCollection<Comment> Comments { get; } = new ReactiveCollection<Comment>();
        public ReactiveCollection<CommentSetting> CommentSettings { get; } = new ReactiveCollection<CommentSetting>();
        public ConcurrentQueue<CommentSetting> CommentSettingsQueue = new ConcurrentQueue<CommentSetting>();
        public PriorityQueue<int, int> UsedLayersQueue = new PriorityQueue<int, int>();
        public ConcurrentQueue<CommentSetting> FixedCommentSettingsQueue = new ConcurrentQueue<CommentSetting>();
        public PriorityQueue<int, int> FixedUsedLayersQueue = new PriorityQueue<int, int>();
        public SynchronizedCollection<bool> IsUsedLayers = new SynchronizedCollection<bool>();
        public ReactiveProperty<double> CurrentHeight { get; } = new ReactiveProperty<double>();
        public ReactiveProperty<double> CurrentWidth { get; } = new ReactiveProperty<double>();
        public ReactiveCommand ShowCommentWindowCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ChangeFullScreenCommand { get; } = new ReactiveCommand();
        public ReactiveCommand ChangeTransparencyCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AddCommentCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AddManyCommentsCommand { get; } = new ReactiveCommand();
        public ReactiveCommand<SizeChangedEventArgs> SizeChangedCommand { get; } = new ReactiveCommand<SizeChangedEventArgs>();

        public SharedViewModel()
        {
            ShowCommentWindowCommand.Subscribe(() => ShowCommentWindow());
            AddCommentCommand.Subscribe(() => ShowComment());
            AddManyCommentsCommand.Subscribe(() => AddManyComments());
            SizeChangedCommand.Subscribe((e) => SizeChanged(e));
            ChangeFullScreenCommand.Subscribe(() => ChangeFullScreen());
            ChangeTransparencyCommand.Subscribe(() => ChangeTransparency());
            _SharedModel = new SharedModel();
            CurrentWidth.Value = 400.0;
            CurrentHeight.Value = 250.0;
            UsedLayersQueue.EnsureCapacity(_CommentSlots * _CommentLayers);
            Enumerable.Range(0, _CommentSlots * _CommentLayers).ToList().ForEach(x => UsedLayersQueue.Enqueue(x, x));
            Enumerable.Range(0, _FixedCommentSlots * _CommentLayers).ToList().ForEach(x => FixedUsedLayersQueue.Enqueue(x, x));
            Enumerable.Repeat(false, _CommentSlots * _CommentLayers).ToList().ForEach(x => IsUsedLayers.Add(x));
        }

        private void SizeChanged(SizeChangedEventArgs e)
        {
            CurrentWidth.Value = e.NewSize.Width;
            CurrentHeight.Value = e.NewSize.Height;
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
                background.Opacity = 0.05;
            } else {
                background.Opacity = 0;
            }
        }

        private async void OnRecordProgressChanged(Record record)
        {
            AddComment(record);
        }

        private void ShowCommentWindow() {
            _SharedModel.ConnectWebSocket(new Progress<Record>(OnRecordProgressChanged));
            CommentWindow commentWindow = new CommentWindow();
            commentWindow.Show();
            Application.Current.MainWindow.Owner = commentWindow;
            _CommentCanvas = commentWindow.MainCanvas;
            _CommentWindow = commentWindow;
        }

        private void ShowComment() {
            i += 1;
            AddComment(new Record() { message = i.ToString(), username = "test" });
        }

        private async void AddManyComments() {
            for (int j = 0; j < 50; j++){
                await Task.Delay(25);
                AddComment(new Record() { message = j.ToString(), username ="test"});
                AddComment(new Record() { message = "インテル長友", username = "test", isFixedComment = true});
            }
        }

        private void AddComment(Record record)
        {
            if (record.isFixedComment) {
                while (true) {
                    if (FixedUsedLayersQueue.TryDequeue(out var emptyLayerIndex, out var priority)) {
                        Storyboard storyboard = new Storyboard();
                        BooleanAnimationUsingKeyFrames booleanAnimationUsingKeyFrames = new BooleanAnimationUsingKeyFrames() {
                            Duration = new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)),
                        };
                        //DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * record.message.Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                        Storyboard.SetTargetProperty(booleanAnimationUsingKeyFrames, new PropertyPath(TextBlock.IsEnabledProperty));
                        storyboard.Children.Add(booleanAnimationUsingKeyFrames);
                        ClockGroup clockGroup = storyboard.CreateClock();
                        clockGroup.Completed += FixedCommentClockGroup_Completed;
                        TextBlock textBlock = new TextBlock() {
                            Text = record.message,
                            FontSize = CurrentHeight.Value / _FixedCommentSlots,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                        };
                        Canvas.SetTop(textBlock, CurrentHeight.Value / _FixedCommentSlots * (emptyLayerIndex % _FixedCommentSlots) + (int)(emptyLayerIndex / _FixedCommentSlots) * CurrentHeight.Value / _FixedCommentSlots / 2);
                        Canvas.SetLeft(textBlock, (CurrentWidth.Value - CurrentHeight.Value / _FixedCommentSlots * record.message.Length) /2);
                        _CommentCanvas.Children.Add(textBlock);
                        storyboard.Begin(textBlock, true);
                        CommentSetting commentSetting = new CommentSetting() {
                            Message = record.message,
                            UserName = record.username,
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
                        DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * record.message.Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                        Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(Canvas.LeftProperty));
                        storyboard.Children.Add(doubleAnimation);
                        ClockGroup clockGroup = storyboard.CreateClock();
                        clockGroup.Completed += CommentClockGroup_Completed;
                        TextBlock textBlock = new TextBlock() {
                            Text = record.message,
                            FontSize = CurrentHeight.Value / _CommentSlots,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 0)),
                        };
                        Canvas.SetTop(textBlock, CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentSlots) * CurrentHeight.Value / _CommentSlots / 2);
                        _CommentCanvas.Children.Add(textBlock);
                        storyboard.Begin(textBlock, true);
                        CommentSetting commentSetting = new CommentSetting() {
                            Message = record.message,
                            UserName = record.username,
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
    }

    public record class Record
    {
        public string username { get; set; }
        public string message { get; set; }
        public bool isFixedComment { get; set; }
    }

    public record class Comment
    {
        public Record Record { get; set; }
        public double From { get; set; }
        public double To { get; set; }
        public double Height { get; set; }
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
