using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace EasyYoloOcr.Example.Wpf;

/// <summary>
/// GPU-accelerated screen capture using DXGI Desktop Duplication.
/// Captures the primary monitor at native resolution with zero GDI overhead.
/// </summary>
public sealed class ScreenCapture : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDXGIOutputDuplication _duplication;
    private readonly ID3D11Texture2D _stagingTexture;

    private byte[]? _buffer;

    public int Width { get; }
    public int Height { get; }

    public ScreenCapture(int adapterIndex = 0, int outputIndex = 0)
    {
        IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        factory.EnumAdapters1((uint)adapterIndex, out IDXGIAdapter1 adapter);

        D3D11.D3D11CreateDevice(
            adapter,
            DriverType.Unknown,
            DeviceCreationFlags.BgraSupport,
            new FeatureLevel[]
            {
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_12_0,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
                FeatureLevel.Level_9_3,
                FeatureLevel.Level_9_2,
                FeatureLevel.Level_9_1
            },
            out _device!,
            out _,
            out _context!).CheckError();

        adapter.EnumOutputs((uint)outputIndex, out IDXGIOutput output);
        var desc = output.Description;
        Width = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
        Height = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;

        var output1 = output.QueryInterface<IDXGIOutput1>();
        _duplication = output1.DuplicateOutput(_device);

        output1.Dispose();
        output.Dispose();
        adapter.Dispose();
        factory.Dispose();

        _stagingTexture = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None
        });
    }

    /// <summary>
    /// Attempt to capture the current screen frame.
    /// Returns BGRA pixel data, or <c>null</c> if no new frame is available.
    /// </summary>
    public byte[]? TryCaptureFrame()
    {
        IDXGIResource? resource = null;
        try
        {
            Result result = _duplication.AcquireNextFrame(0u, out _, out resource);
            if (result.Failure || resource == null)
                return null;

            using var screenTexture = resource.QueryInterface<ID3D11Texture2D>();
            _context.CopyResource(_stagingTexture, screenTexture);

            MappedSubresource mapped = _context.Map(_stagingTexture, 0u, MapMode.Read,
                Vortice.Direct3D11.MapFlags.None);
            try
            {
                int stride = Width * 4;
                _buffer ??= new byte[stride * Height];
                for (int row = 0; row < Height; row++)
                {
                    IntPtr srcPtr = IntPtr.Add(mapped.DataPointer, (int)(row * mapped.RowPitch));
                    Marshal.Copy(srcPtr, _buffer, row * stride, stride);
                }
                return _buffer;
            }
            finally
            {
                _context.Unmap(_stagingTexture, 0u);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            resource?.Dispose();
            try { _duplication.ReleaseFrame(); } catch { }
        }
    }

    public void Dispose()
    {
        _stagingTexture?.Dispose();
        _duplication?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
    }
}
