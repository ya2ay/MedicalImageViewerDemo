using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows;
using System.Windows.Media;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinUserControl = System.Windows.Controls.UserControl;
using WinSize = System.Windows.Size;
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
            _mode = DirectXRenderer.RenderMode.None_Slice;
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

        public void SetDicomSeries(DicomSeries series)
        {
            _series = series;
            _renderer?.SetSeries(series, _mode); // 通知渲染器更新数据
        }

        public void SetDicomSeries(DicomSeries series, DirectXRenderer.RenderMode mode)
        {
            _mode = mode;
            SetDicomSeries(series);
        }

        protected override WinSize MeasureOverride(WinSize availableSize)
        {
            // 取可用空间中较小的一维，形成正方形
            double min = Math.Min(availableSize.Width, availableSize.Height);
            return new WinSize(min, min);
        }

        protected override WinSize ArrangeOverride(WinSize finalSize)
        {
            // 实际排列也使用正方形
            double size = Math.Min(finalSize.Width, finalSize.Height);
            return base.ArrangeOverride(new WinSize(size, size));
        }
        // D3DHost.xaml.cs —— 在类中添加
        public int CurrentSliceIndex
        {
            get => _renderer?.CurrentSliceIndex ?? 0;
            set
            {
                if (_renderer != null)
                {
                    _renderer.CurrentSliceIndex = value;
                }
            }
        }

        // 可选：添加 WW/WL 控制
        public (float ww, float wl) Windowing
        {
            get => _renderer != null ? (_renderer.WindowWidth, _renderer.WindowLevel) : (400, 40);
            set
            {
                if (_renderer != null)
                {
                    _renderer.WindowWidth = value.ww;
                    _renderer.WindowLevel = value.wl;
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            int width = (int)host.ActualWidth;
            int height = (int)host.ActualHeight;
            _renderer = new DirectXRenderer(_panel.Handle, width, height);
            CompositionTarget.Rendering += OnRendering;
            // 监听尺寸变化
            host.SizeChanged += OnHostSizeChanged;
        }

        private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_renderer != null)
            {
                // 更新 Direct3D 渲染目标尺寸
                _renderer.Resize((uint)e.NewSize.Width, (uint)e.NewSize.Height);
            }
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
    }
}
