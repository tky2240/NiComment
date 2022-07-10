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
        private readonly int _CommentLayers = 2;
        private readonly int _AnimationDurationSecond = 4;
        private Canvas _CommentCanvas = null;
        public ReactiveCollection<Comment> Comments { get; } = new ReactiveCollection<Comment>();
        public ReactiveCollection<CommentSetting> CommentSettings { get; } = new ReactiveCollection<CommentSetting>();
        //public ConcurrentQueue<CommentSetting> CommentSettingsQueue = new ConcurrentQueue<CommentSetting>();
        public PriorityQueue<int, int> UsedLayersQueue = new PriorityQueue<int, int>();
        public SynchronizedCollection<bool> IsUsedLayers = new SynchronizedCollection<bool>();
        public ReactiveProperty<double> CurrentHeight { get; } = new ReactiveProperty<double>();
        public ReactiveProperty<double> CurrentWidth { get; } = new ReactiveProperty<double>();
        public ReactiveCommand ShowCommentWindowCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AddCommentCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AddManyCommentsCommand { get; } = new ReactiveCommand();
        public ReactiveCommand<SizeChangedEventArgs> SizeChangedCommand { get; } = new ReactiveCommand<SizeChangedEventArgs>();

        public SharedViewModel()
        {
            ShowCommentWindowCommand.Subscribe(() => ShowCommentWindow());
            AddCommentCommand.Subscribe(() => AddComment());
            AddManyCommentsCommand.Subscribe(() => AddManyComments());
            SizeChangedCommand.Subscribe((e) => SizeChanged(e));
            _SharedModel = new SharedModel();
            CurrentWidth.Value = 400.0;
            CurrentHeight.Value = 250.0;
            UsedLayersQueue.EnsureCapacity(_CommentSlots * _CommentLayers);
            Enumerable.Range(0, _CommentSlots * _CommentLayers).ToList().ForEach(x => UsedLayersQueue.Enqueue(x, x));
            Enumerable.Repeat(false, _CommentSlots * _CommentLayers).ToList().ForEach(x => IsUsedLayers.Add(x));
        }

        private void SizeChanged(SizeChangedEventArgs e)
        {
            CurrentWidth.Value = e.NewSize.Width;
            CurrentHeight.Value = e.NewSize.Height;
        }

        private async void OnRecordProgressChanged(Record record)
        {
            int emptyLayerIndex = IsUsedLayers.ToList().FindIndex(x => !x);
            if (emptyLayerIndex != -1) {
                IsUsedLayers[emptyLayerIndex] = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * i.ToString().Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("Canvas.Left"));
                storyboard.Children.Add(doubleAnimation);
                ClockGroup clockGroup = storyboard.CreateClock();
                storyboard.Completed += Storyboard_Completed;
                TextBlock textBlock = new TextBlock() {
                    Text = record.message,
                    FontSize = 30,
                };
                Canvas.SetTop(textBlock, CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2);
                _CommentCanvas.Children.Add(textBlock);
                storyboard.Begin(textBlock, true);
                CommentSetting commentSetting = new CommentSetting() {
                    Message = record.message,
                    UserName = record.username,
                    From = CurrentWidth.Value,
                    To = -30 * i.ToString().Length,
                    Top = CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2,
                    UsdeLayerIndex = emptyLayerIndex,
                    Storyboard = storyboard,
                    TextColor = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    FontSize = 30,
                    ClockGroup = clockGroup,
                };
                CommentSettings.AddOnScheduler(commentSetting);
            } else {
                var oldestCommentSetting = CommentSettings.ElementAt(0);
                CommentSettings.RemoveAtOnScheduler(0);
                oldestCommentSetting.Storyboard.Stop();
                IsUsedLayers[emptyLayerIndex] = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * i.ToString().Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("Canvas.Left"));
                storyboard.Children.Add(doubleAnimation);
                storyboard.Completed += Storyboard_Completed;
                storyboard.Begin();
                CommentSetting commentSetting = new CommentSetting() {
                    Message = record.message,
                    UserName = record.username,
                    From = CurrentWidth.Value,
                    To = -30 * i.ToString().Length,
                    Top = CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2,
                    UsdeLayerIndex = oldestCommentSetting.UsdeLayerIndex,
                    Storyboard = storyboard,
                    TextColor = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    FontSize = 30,
                };
                CommentSettings.AddOnScheduler(commentSetting);
            }
        }

        private void ShowCommentWindow() {
            _SharedModel.ConnectWebSocket(new Progress<Record>(OnRecordProgressChanged));
            CommentWindow commentWindow = new CommentWindow();
            commentWindow.Show();
            _CommentCanvas = commentWindow.MainCanvas;
        }

        private async void AddManyComments() {
            for (int j = 0; j < 50; j++){
                await Task.Delay(50);
                AddComment();
            }
        }

        private void AddComment()
        {
            i += 1;
            var temp = "0";
            int emptyLayerIndex = IsUsedLayers.ToList().FindIndex(x => !x);
            if (UsedLayersQueue.TryDequeue(out var index, out var priority)) {
                sUsedLayers[emptyLayerIndex] = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * i.ToString().Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(Canvas.LeftProperty));
                storyboard.Children.Add(doubleAnimation);
                ClockGroup clockGroup = storyboard.CreateClock();
                clockGroup.Completed += ClockGroup_Completed;
                TextBlock textBlock = new TextBlock() {
                    Text = i.ToString(),
                    FontSize = 30,
                };
                Canvas.SetTop(textBlock, CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2);
                _CommentCanvas.Children.Add(textBlock);
                storyboard.Begin(textBlock, true);
                CommentSetting commentSetting = new CommentSetting() {
                    Message = i.ToString(),
                    UserName = "test",
                    From = CurrentWidth.Value,
                    To = -30 * i.ToString().Length,
                    Top = CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2,
                    UsdeLayerIndex = emptyLayerIndex,
                    Storyboard = storyboard,
                    TextColor = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    FontSize = 30,
                    ClockGroup = clockGroup,
                    TextBlock = textBlock,
                };
                CommentSettingsQueue.Enqueue(commentSetting);
            }
            if (emptyLayerIndex != -1){
                IsUsedLayers[emptyLayerIndex] = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * i.ToString().Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(Canvas.LeftProperty));
                storyboard.Children.Add(doubleAnimation);
                ClockGroup clockGroup = storyboard.CreateClock();
                clockGroup.Completed += ClockGroup_Completed;
                TextBlock textBlock = new TextBlock() {
                    Text = i.ToString(),
                    FontSize = 30,
                };
                Canvas.SetTop(textBlock, CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2);
                _CommentCanvas.Children.Add(textBlock);
                storyboard.Begin(textBlock, true);
                CommentSetting commentSetting = new CommentSetting() {
                    Message = i.ToString(),
                    UserName = "test",
                    From = CurrentWidth.Value,
                    To = -30 * i.ToString().Length,
                    Top = CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2,
                    UsdeLayerIndex = emptyLayerIndex,
                    Storyboard = storyboard,
                    TextColor = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    FontSize = 30,
                    ClockGroup = clockGroup,
                    TextBlock = textBlock,
                };
                CommentSettingsQueue.Enqueue(commentSetting);
                //CommentSettings.AddOnScheduler(commentSetting);
            } else {
                CommentSetting oldestCommentSetting;
                while (true) {
                    if (CommentSettingsQueue.TryDequeue(out oldestCommentSetting)) {
                        break;
                    }
                }
                oldestCommentSetting.Storyboard.Stop();
                oldestCommentSetting.TextBlock.Visibility = Visibility.Collapsed;
                _CommentCanvas.Children.Remove(oldestCommentSetting.TextBlock);
                //IsUsedLayers[emptyLayerIndex] = true;
                Storyboard storyboard = new Storyboard();
                DoubleAnimation doubleAnimation = new DoubleAnimation(CurrentWidth.Value, -30 * i.ToString().Length, new Duration(TimeSpan.FromSeconds(_AnimationDurationSecond)));
                Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(Canvas.LeftProperty));
                storyboard.Children.Add(doubleAnimation);
                ClockGroup clockGroup = storyboard.CreateClock();
                clockGroup.Completed += ClockGroup_Completed;
                TextBlock textBlock = new TextBlock() {
                    Text = i.ToString(),
                    FontSize = 30,
                };
                Canvas.SetTop(textBlock, CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2);
                _CommentCanvas.Children.Add(textBlock);
                storyboard.Begin(textBlock, true);
                CommentSetting commentSetting = new CommentSetting() {
                    Message = i.ToString(),
                    UserName = "test",
                    From = CurrentWidth.Value,
                    To = -30 * i.ToString().Length,
                    Top = CurrentHeight.Value / _CommentSlots * (emptyLayerIndex % _CommentSlots) + (int)(emptyLayerIndex / _CommentLayers) * CurrentHeight.Value / _CommentSlots / 2,
                    UsdeLayerIndex = oldestCommentSetting.UsdeLayerIndex,
                    Storyboard = storyboard,
                    TextColor = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    FontSize = 30,
                    TextBlock = textBlock,
                };
                CommentSettingsQueue.Enqueue(commentSetting);
            }
        }

        private void ClockGroup_Completed(object? sender, EventArgs e) {
            while(true) {
                if (CommentSettingsQueue.TryDequeue(out var oldestCommentSetting)) {
                    IsUsedLayers[oldestCommentSetting.UsdeLayerIndex] = false;
                    _CommentCanvas.Children.Remove(oldestCommentSetting.TextBlock);
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
        public double From { get; set; }
        public double To { get; set; }
        public double Height { get; set; }
        public double Top { get; set; }
        public int UsdeLayerIndex { get; set; }
        public double FontSize { get; set; }
        public SolidColorBrush TextColor { get; set; }
        public Storyboard Storyboard { get; set; }
        public ClockGroup ClockGroup { get; set; }
        public TextBlock TextBlock { get; set; }
    }
}
