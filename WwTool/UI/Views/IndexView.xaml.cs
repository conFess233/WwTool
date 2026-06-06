using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WwTool.UI.Views
{
    /// <summary>
    /// IndexView.xaml 的交互逻辑
    /// </summary>
    public partial class IndexView : UserControl
    {
        public IndexView()
        {
            InitializeComponent();
        }

        private bool _isDragging = false;
        private Point _clickPosition;

        private void OnRoleCardMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                _isDragging = true;
                _clickPosition = e.GetPosition(element);
                element.CaptureMouse();

                if (double.IsNaN(Canvas.GetLeft(element)))
                {

                    double currentLeft = DragCanvas.ActualWidth - element.ActualWidth - Canvas.GetRight(element);
                    Canvas.SetLeft(element, currentLeft);

                    element.ClearValue(Canvas.RightProperty);
                }
            }
        }

        private void OnRoleCardMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is FrameworkElement element)
            {
                Point currentMousePosition = e.GetPosition(DragCanvas);

                double newLeft = currentMousePosition.X - _clickPosition.X;
                double newTop = currentMousePosition.Y - _clickPosition.Y;

                // 边界控制
                if (newLeft < 0) newLeft = 0;
                if (newTop < 0) newTop = 0;
                if (newLeft + element.ActualWidth > DragCanvas.ActualWidth)
                    newLeft = DragCanvas.ActualWidth - element.ActualWidth;
                if (newTop + element.ActualHeight > DragCanvas.ActualHeight)
                    newTop = DragCanvas.ActualHeight - element.ActualHeight;

                Canvas.SetLeft(element, newLeft);
                Canvas.SetTop(element, newTop);
            }
        }

        private void OnRoleCardMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && sender is FrameworkElement element)
            {
                _isDragging = false;
                element.ReleaseMouseCapture();
            }
        }
    }
}
