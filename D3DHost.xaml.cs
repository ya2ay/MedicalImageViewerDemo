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
using System.Windows.Navigation;
using System.Windows.Shapes;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinUserControl = System.Windows.Controls.UserControl;
namespace MedicalRenderDemo
{
    /// <summary>
    /// D3DHost.xaml 的交互逻辑
    /// </summary>
    public partial class D3DHost : WinUserControl, IDisposable
    {
        private WinFormsPanel _panel;
        private DirectXRenderer? _renderer;
        private DicomSeries? _series;
        private DirectXRenderer.RenderMode _mode;

        public D3DHost()
        {
            InitializeComponent();
            _panel = new WinFormsPanel();
            host.Child = _panel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public D3DHost(DirectXRenderer.RenderMode mode)
        {
            _mode = mode;
            InitializeComponent();
            _panel = new WinFormsPanel();
            host.Child = _panel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            int width = (int)host.ActualWidth;
            int height = (int)host.ActualHeight;
            _renderer = new DirectXRenderer(_panel.Handle, width, height);
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderer?.Dispose();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            _renderer?.Render();
        }

        public void Dispose()
        {
            _renderer?.Dispose();
        }

        public void SetDicomSeries(DicomSeries series, DirectXRenderer.RenderMode mode)
        {
            _mode = mode;
            _series = series;
            _renderer?.SetSeries(series, _mode); // 通知渲染器更新数据
        }

    }
}
