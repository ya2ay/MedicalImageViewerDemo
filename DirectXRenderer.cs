using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;

namespace MedicalRenderDemo
{
    public class DirectXRenderer : IDisposable
    {
        public enum RenderMode
        {
            AxialSlice,
            CoronalSlice,
            SagittalSlice,
            Volume3D
        }

        private readonly IntPtr _hwnd;
        public int Width { get; }
        public int Height { get; }

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private IDXGISwapChain1? _swapChain;
        private ID3D11RenderTargetView? _renderTargetView;
        private ID3D11ShaderResourceView? _textureView;
        private ID3D11Texture2D? _texture;
        private DicomSeries? _series;
        private RenderMode _renderMode;

        public DirectXRenderer(IntPtr hwnd, int width, int height)
        {
            _hwnd = hwnd;
            Width = width;
            Height = height;
            Initialize();
        }

        private void Initialize()
        {
            // ✅ 正确使用 CreateDeviceFlags
            DeviceCreationFlags deviceCreationFlags = DeviceCreationFlags.None;
#if DEBUG
            deviceCreationFlags |= DeviceCreationFlags.Debug;
#endif

            // 调用 D3D11CreateDevice
            Result result = D3D11.D3D11CreateDevice(
                adapter: null,                          // IDXGIAdapter*，null 表示默认适配器
                driverType: DriverType.Hardware,        // 使用硬件加速
                                                        //software: IntPtr.Zero,                  // 仅当 driverType == DriverType.Software 时有效
                flags: deviceCreationFlags,             // DeviceCreationFlags（注意命名！）
                featureLevels: new[] { FeatureLevel.Level_11_0 }, // 支持的特性级别
                device: out _device,                    // 输出设备
                featureLevel: out _,                   // 实际使用的特性级别（可忽略）
                immediateContext: out _context                   // 输出上下文
            );

            result.CheckError();

            // 创建交换链描述（使用 SwapChainDescription1）
            var swapChainDesc = new SwapChainDescription1
            {
                Width = (uint)Width,
                Height = (uint)Height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Ignore
            };

            // 获取 DXGI 工厂
            // 1. 将 D3D11 设备转为 DXGI 设备
            var dxgiDevice = _device.QueryInterface<IDXGIDevice>();

            // 2. 从 DXGI 设备获取适配器
            var adapter = dxgiDevice.GetAdapter();

            // 3. 从适配器获取 DXGI 工厂（升级到 Factory2）
            var dxgiFactory = adapter.GetParent<IDXGIFactory2>();
            _swapChain = dxgiFactory.CreateSwapChainForHwnd(_device, _hwnd, swapChainDesc);

            CreateRenderTargetView();
            CreateTexture();
        }

        public void SetSeries(DicomSeries series, RenderMode mode)
        {
            if (series == null) throw new ArgumentNullException(nameof(series));

            _series = series;
            _renderMode = mode;

            // TODO: 根据 mode 和 series 构建体数据、创建纹理、设置着色器常量等
            RebuildResources();
        }

        private void RebuildResources()
        {
            if (_series == null) return;

            switch (_renderMode)
            {
                case RenderMode.AxialSlice:
                    // 加载第一帧作为默认切片
                    break;
                case RenderMode.CoronalSlice:
                case RenderMode.SagittalSlice:
                case RenderMode.Volume3D:
                    // 构建 3D 体数据（Volume）
                    BuildVolume();
                    break;
            }
        }

        private void BuildVolume()
        {
            // TODO: 将 _series.Files 合并为 3D short[] 数组
            // 并上传到 GPU 纹理（如 Texture3D）
        }

        public void Render()
        {
            if (_series == null) return;

            // 根据 _renderMode 执行不同渲染逻辑
            switch (_renderMode)
            {
                case RenderMode.AxialSlice:
                    RenderAxial();
                    break;
                case RenderMode.CoronalSlice:
                    RenderCoronal();
                    break;
                case RenderMode.SagittalSlice:
                    RenderSagittalSlice();
                    break;
                case RenderMode.Volume3D:
                    RenderVolume3D();
                    break;
                    // ... 其他
            }

            // Present
        }

        private void RenderAxial()
        {
            // 渲染当前 axial 切片（例如第 0 帧）
            if (_context == null || _renderTargetView == null) return;

            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.1f, 0.2f, 0.3f, 1.0f));
            _swapChain!.Present(1, PresentFlags.None);
        }

        private void RenderCoronal()
        {
            // 渲染 coronal 切面（需体数据支持）
            if (_context == null || _renderTargetView == null) return;

            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.8f, 0.2f, 0.3f, 1.0f));
            _swapChain!.Present(1, PresentFlags.None);
        }

        private void RenderSagittalSlice()
        {
            // 渲染 coronal 切面（需体数据支持）
            if (_context == null || _renderTargetView == null) return;

            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.1f, 0.2f, 0.5f, 1.0f));
            _swapChain!.Present(1, PresentFlags.None);
        }

        private void RenderVolume3D()
        {
            // 渲染 coronal 切面（需体数据支持）
            if (_context == null || _renderTargetView == null) return;

            _context.ClearRenderTargetView(_renderTargetView, new Color4(0.1f, 0.3f, 0.3f, 1.0f));
            _swapChain!.Present(1, PresentFlags.None);
        }

        private void CreateRenderTargetView()
        {
            if (_swapChain == null) return;
            var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _renderTargetView = _device!.CreateRenderTargetView(backBuffer);
            backBuffer.Dispose();
        }

        public void Resize(uint width, uint height)
        {
            _renderTargetView?.Dispose();
            _swapChain.ResizeBuffers(2, width, height, Format.Unknown, SwapChainFlags.None);
            CreateRenderTargetView();
        }
        private void CreateTexture()
        {
            var texDesc = new Texture2DDescription
            {
                Width = (uint)Width,
                Height = (uint)Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            _texture = _device!.CreateTexture2D(texDesc);
            _textureView = _device.CreateShaderResourceView(_texture);
        }

        public void UpdateTexture(byte[] pixelData)
        {
            if (_texture == null || _context == null) return;

            // 可选：验证数据大小（调试用）
            if (pixelData.Length != Width * Height)
                throw new ArgumentException("pixelData size must be Width * Height for R8_UNorm format.");
            //var box = new Box(0, 0, 0, Width, Height, 1);
            _context.UpdateSubresource(pixelData, _texture);
        }

        public void Dispose()
        {
            _renderTargetView?.Dispose();
            _textureView?.Dispose();
            _texture?.Dispose();
            _swapChain?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
        }
    }
}
