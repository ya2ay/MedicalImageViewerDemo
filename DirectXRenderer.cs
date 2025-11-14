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
using System.Numerics;
using System.Windows;
using Vortice;
using Vortice.D3DCompiler;
using System.Diagnostics;

namespace MedicalRenderDemo
{
    public class DirectXRenderer : IDisposable
    {
        public enum RenderMode
        {
            None_Slice,
            AxialSlice,
            CoronalSlice,
            SagittalSlice,
            Volume3D
        }

        private readonly IntPtr _hwnd;
        private int _width;
        private int _height;
        public int Width
        {
            get => _width;
            private set => _width = value; // 允许类内部修改
        }

        public int Height
        {
            get => _height;
            private set => _height = value;
        }

        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;
        private IDXGISwapChain1? _swapChain;
        private ID3D11RenderTargetView? _renderTargetView;

        // 2D texture for slice display
        private ID3D11Texture2D? _sliceTexture;
        private ID3D11ShaderResourceView? _sliceTextureView;

        // 3D volume texture
        private ID3D11Texture3D? _volumeTexture;
        private ID3D11ShaderResourceView? _volumeTextureView;

        // Shaders and pipeline
        private ID3D11VertexShader? _vertexShader;
        private ID3D11PixelShader? _pixelShader;
        private ID3D11InputLayout? _inputLayout;
        private ID3D11Buffer? _vertexBuffer;
        private ID3D11SamplerState? _sampler;

        private DicomSeries? _series;
        private DicomSeries.VolumeData? _volumeData;
        private RenderMode _renderMode = RenderMode.None_Slice;

        // Rendering parameters
        public int CurrentSliceIndex { get; set; } = 0;
        public float WindowWidth { get; set; } = 400;   // CT default
        public float WindowLevel { get; set; } = 40;    // CT soft tissue
        private const int _bufferCount = 2;

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
                immediateContext: out ID3D11DeviceContext baseContext                   // 输出上下文
            );

            if (!result.Success)
            {
                throw new Exception($"Failed to create D3D11 device: {result}");
            }

            // Step 2: 尝试升级到 ID3D11DeviceContext3
            ID3D11DeviceContext3? context3 = baseContext.QueryInterface<ID3D11DeviceContext3>();
            if (context3 != null)
            {
                _context = context3; // 使用高级接口
            }
            else
            {
                _context = baseContext; // 回退到基础接口
            }

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
                BufferCount = _bufferCount,
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
            CreateShadersAndBuffers();
            CreateSamplers();
            //CreateTexture();
        }

        private void CreateShadersAndBuffers()
        {
            // Fullscreen quad vertices (NDC space)
            var vertices = new[]
            {
                new Vector4(-1, -1, 0, 1),
                new Vector4(-1,  1, 0, 1),
                new Vector4( 1,  1, 0, 1),
                new Vector4( 1, -1, 0, 1)
            };
            var vertexBytes = MemoryMarshal.AsBytes(vertices.AsSpan()).ToArray();

            var vbDesc = new BufferDescription
            {
                ByteWidth = (uint)vertexBytes.Length,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.VertexBuffer
            };

            _vertexBuffer = _device!.CreateBuffer(vbDesc);

            // HLSL shaders as strings
            string vsCode = @"
                float4 main(float4 pos : POSITION) : SV_POSITION
                {
                    return pos;
                }";

            string psCode = @"
                Texture2D g_Texture : register(t0);
                SamplerState g_Sampler : register(s0);

                float4 main(float4 pos : SV_POSITION) : SV_TARGET
                {
                    float2 uv = pos.xy / pos.w;
                    uv.y = 1.0f - uv.y; // flip Y
                    return g_Texture.Sample(g_Sampler, uv);
                }";


            Result vsResult = Compiler.Compile(
                shaderSource: vsCode,
                entryPoint: "main",
                sourceName: "FullScreenVS.hlsl",
                profile: "vs_5_0",
                out Blob vsBlob,
                out Blob vsErrorBlob
            );

            Result psResult = Compiler.Compile(
                shaderSource: psCode,
                entryPoint: "main",
                sourceName: "FullScreenPS.hlsl",
                profile: "ps_5_0",
                out Blob psBlob,
                out Blob psErrorBlob
            );

            if (vsResult.Code == null || psResult.Code == null)
            {
                throw new InvalidOperationException(
                    $"Shader compilation failed:\nVS: {vsErrorBlob.AsString()}\nPS: {psErrorBlob.AsString()}");
            }

            _vertexShader = _device.CreateVertexShader(vsBlob.AsSpan());
            _pixelShader = _device.CreatePixelShader(psBlob.AsSpan());

            var inputElement = new Vortice.Direct3D11.InputElementDescription[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0)
            };
            _inputLayout = _device.CreateInputLayout(inputElement, vsBlob.AsSpan());
        }

        private void CreateSamplers()
        {
            var samplerDesc = new SamplerDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunc = ComparisonFunction.Never,
                BorderColor = new Color4(0, 0, 0, 0)
            };
            _sampler = _device!.CreateSamplerState(samplerDesc);
        }

        public void SetSeries(DicomSeries series, RenderMode mode)
        {
            if (series == null) throw new ArgumentNullException(nameof(series));

            _series = series;
            _renderMode = mode;
            _volumeData = series.BuildVolume(); // Build CPU volume
            RebuildResources();
        }

        private void RebuildResources()
        {
            if (_volumeData == null || _device == null) return;

            // Create 3D texture (for future use in GPU-based slicing)
            CreateVolumeTexture();

            // For now, 2D slices are extracted on CPU
            switch (_renderMode)
            {
                case RenderMode.AxialSlice:
                    CurrentSliceIndex = Math.Clamp(CurrentSliceIndex, 0, _volumeData.Depth - 1);
                    break;
                case RenderMode.CoronalSlice:
                    CurrentSliceIndex = Math.Clamp(CurrentSliceIndex, 0, _volumeData.Height - 1);
                    break;
                case RenderMode.SagittalSlice:
                    CurrentSliceIndex = Math.Clamp(CurrentSliceIndex, 0, _volumeData.Width - 1);
                    break;
            }
        }

        private void CreateVolumeTexture()
        {
            if (_volumeData == null) return;

            _volumeTexture?.Dispose();
            _volumeTextureView?.Dispose();

            var texDesc = new Texture3DDescription
            {
                Width = (uint)_volumeData.Width,
                Height = (uint)_volumeData.Height,
                Depth = (uint)_volumeData.Depth,
                MipLevels = 1,
                Format = Format.R16_SInt,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            var DataPointer = Marshal.UnsafeAddrOfPinnedArrayElement(_volumeData.Voxels, 0);
            var RowPitch = _volumeData.Width * sizeof(short);
            var SlicePitch = _volumeData.Width * _volumeData.Height * sizeof(short);

            var initData = new SubresourceData(DataPointer, (uint)RowPitch, (uint)SlicePitch);
            _volumeTexture = _device!.CreateTexture3D(texDesc, initData);
            _volumeTextureView = _device.CreateShaderResourceView(_volumeTexture);
        }

        private byte[] ExtractSlice()
        {
            if (_volumeData == null || _series == null) 
                return new byte[0];

            int w = _volumeData.Width;
            int h = _volumeData.Height;
            int d = _volumeData.Depth;
            var voxels = _volumeData.Voxels;

            byte[] slice = [];
            ushort[] sourceSlice = Array.Empty<ushort>();

            switch (_renderMode)
            {
                case RenderMode.AxialSlice:
                    w = _volumeData.Width;
                    h = _volumeData.Height;
                    int axialIndex = Math.Clamp(CurrentSliceIndex, 0, _volumeData.Depth - 1);
                    sourceSlice = new ushort[w * h];
                    for (int i = 0; i < w * h; i++)
                        sourceSlice[i] = _volumeData.Voxels[i + axialIndex * w * h];
                    break;

                case RenderMode.CoronalSlice:
                    w = _volumeData.Width;
                    h = _volumeData.Depth;
                    int coronalIndex = Math.Clamp(CurrentSliceIndex, 0, _volumeData.Height - 1);
                    sourceSlice = new ushort[w * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            sourceSlice[x + y * w] = _volumeData.Voxels[x + coronalIndex * w + y * w * _volumeData.Height];
                    break;

                case RenderMode.SagittalSlice:
                    w = _volumeData.Height;
                    h = _volumeData.Depth;
                    int sagittalIndex = Math.Clamp(CurrentSliceIndex, 0, _volumeData.Width - 1);
                    sourceSlice = new ushort[w * h];
                    for (int y = 0; y < h; y++)
                        for (int z = 0; z < w; z++)
                            sourceSlice[z + y * w] = _volumeData.Voxels[sagittalIndex + z * _volumeData.Width + y * _volumeData.Width * _volumeData.Height];
                    break;

                default:
                    return new byte[w * h];
            }
            //窗宽窗位映射到 [0, 255]
            var result = new byte[sourceSlice.Length];
            int wc = _series.WindowCenter;
            int ww = _series.WindowWidth;
            float min = wc - ww / 2.0f;
            float max = wc + ww / 2.0f;
            float scale = 255.0f / (max - min);

            for (int i = 0; i < sourceSlice.Length; i++)
            {
                float val = sourceSlice[i];
                val = Math.Clamp((val - min) * scale, 0, 255);
                result[i] = (byte)val;
            }
            return Enumerable.Repeat((byte)255, sourceSlice.Length).ToArray(); // 强制白色
            //return result;
        }

        private void EnsureSliceTextureSize(int width, int height)
        {
            if (_sliceTexture != null &&
                (_sliceTexture.Description.Width != width || _sliceTexture.Description.Height != height))
            {
                _sliceTextureView?.Dispose();
                _sliceTexture?.Dispose();
                _sliceTexture = null;
            }

            if (_sliceTexture == null)
            {
                var desc = new Texture2DDescription
                {
                    Width = (uint)width,
                    Height = (uint)height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.R8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                    CPUAccessFlags = CpuAccessFlags.None,
                    MiscFlags = ResourceOptionFlags.None
                };
                _sliceTexture = _device!.CreateTexture2D(desc);
                _sliceTextureView = _device.CreateShaderResourceView(_sliceTexture);
            }
        }

        public void Render()
        {
            if (_context == null || _renderTargetView == null || _volumeData == null) return;
            if (_sampler == null || _volumeData == null) return;

            _context.ClearRenderTargetView(_renderTargetView, new Color4(1, 0, 0, 1)); // 红色

            var sliceData = ExtractSlice();
            if (sliceData.Length == 0) return;

            int sliceWidth = _volumeData.Width;
            int sliceHeight = _volumeData.Height;

            switch (_renderMode)
            {
                case RenderMode.AxialSlice:
                    sliceWidth = _volumeData.Width;
                    sliceHeight = _volumeData.Height;
                    break;
                case RenderMode.CoronalSlice:
                    sliceWidth = _volumeData.Width;
                    sliceHeight = _volumeData.Depth;
                    break;
                case RenderMode.SagittalSlice:
                    sliceWidth = _volumeData.Height;
                    sliceHeight = _volumeData.Depth;
                    break;
            }

            EnsureSliceTextureSize(sliceWidth, sliceHeight);

            // Update 2D texture
            _context.UpdateSubresource(sliceData, _sliceTexture!);

            // Render full-screen quad
            _context.ClearRenderTargetView(_renderTargetView, new Color4(0, 0, 0, 1));
            _context.OMSetRenderTargets(_renderTargetView);
            _context.VSSetShader(_vertexShader);
            _context.PSSetShader(_pixelShader);
            _context.IASetInputLayout(_inputLayout);
            _context.PSSetShaderResources(0, new[] { _sliceTextureView });
            _context.PSSetSamplers(0, new[] { _sampler });
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
            ID3D11Buffer?[] buffers = { _vertexBuffer };
            uint[] strides = { 16 };   // Vector4 = 16 bytes
            uint[] offsets = { 0 };
            _context.IASetVertexBuffers(0, buffers, strides, offsets);
            _context.Draw(4, 0);

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
            if (width == 0 || height == 0)
                return;

            _renderTargetView?.Dispose();
            _swapChain?.ResizeBuffers(_bufferCount, width, height, Format.Unknown, SwapChainFlags.None);
            Width = (int)width;
            Height = (int)height;
            CreateRenderTargetView();
        }

        public void Dispose()
        {
            _renderTargetView?.Dispose();
            _sliceTextureView?.Dispose();
            _sliceTexture?.Dispose();
            _volumeTextureView?.Dispose();
            _volumeTexture?.Dispose();
            _vertexBuffer?.Dispose();
            _inputLayout?.Dispose();
            _vertexShader?.Dispose();
            _pixelShader?.Dispose();
            _sampler?.Dispose();
            _swapChain?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
        }
    }
}
