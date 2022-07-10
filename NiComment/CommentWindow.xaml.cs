using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NiComment
{
    /// <summary>
    /// CommentWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class CommentWindow : Window
    {
        public CommentWindow()
        {
            InitializeComponent();
            this.MouseLeftButtonDown += (sender, e) => { this.DragMove(); };
            var hoge = new System.Windows.Media.Animation.Storyboard();
        }
    }
}
