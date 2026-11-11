using System.Diagnostics;
using System.Runtime.Versioning;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.MediaFoundation;

namespace WwTool.UI.Services.Video;

/// <summary>解码出的一帧：BGRX（每像素 4 字节），<paramref name="Pitch"/> 可能大于宽度 × 4。仅在回调期间有效。</summary>
public delegate void VideoFrameCallback(IntPtr scan0, int pitch, int width, int height);

/// <summary>
/// 用 Windows Media Foundation 循环播放一个视频文件（动态背景）：
/// 尽量用 D3D11 硬件解码，由 Source Reader 的高级视频处理把画面转成 RGB32 并直接缩放到 <see cref="SetTargetSize"/> 给定的尺寸；
/// 在独立线程按时间戳输出，帧率不超过 <see cref="MaxFps"/>，到结尾回到开头。暂停时保留在当前位置。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class MfVideoLoop : IDisposable
{
    public const int MaxFps = 30;

    private static readonly object StartupLock = new();

    /// <summary>MF_LOW_LATENCY。</summary>
    private static readonly Guid LowLatency = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");
    private static int _startupCount;

    private readonly string _path;
    private readonly VideoFrameCallback _onFrame;
    private readonly Action<Exception>? _onError;
    private readonly ManualResetEventSlim _running = new(false);
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;

    private int _requestedWidth, _requestedHeight;
    private volatile bool _sizeChanged;

    /// <summary>视频原始尺寸（打开后可用）。</summary>
    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }

    /// <summary>是否用上了硬件解码（D3D11 设备管理器创建成功）。</summary>
    public bool IsHardwareAccelerated { get; private set; }

    public MfVideoLoop(string path, int width, int height, VideoFrameCallback onFrame, Action<Exception>? onError = null)
    {
        _path = path;
        _onFrame = onFrame;
        _onError = onError;
        _requestedWidth = Even(width);
        _requestedHeight = Even(height);
        _thread = new Thread(Run) { IsBackground = true, Name = "WwTool video backdrop", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    /// <summary>要盖住的区域尺寸（物理像素）；实际输出保持视频宽高比、刚好盖住该区域且不超过原尺寸。窗口大小变化后调用，下一帧起生效。</summary>
    public void SetTargetSize(int width, int height)
    {
        width = Even(width);
        height = Even(height);
        if (width == Volatile.Read(ref _requestedWidth) && height == Volatile.Read(ref _requestedHeight)) return;
        Volatile.Write(ref _requestedWidth, width);
        Volatile.Write(ref _requestedHeight, height);
        _sizeChanged = true;
    }

    public void Play() => _running.Set();

    public void Pause() => _running.Reset();

    public void Dispose()
    {
        _stop.Cancel();
        _running.Set();
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(3));
        _running.Dispose();
        _stop.Dispose();
    }

    private static int Even(int value) => Math.Max(2, value & ~1);

    private void Run()
    {
        bool started = false;
        ID3D11Device? device = null;
        IMFDXGIDeviceManager? manager = null;
        IMFSourceReader? reader = null;
        try
        {
            lock (StartupLock)
            {
                if (_startupCount++ == 0) MediaFactory.MFStartup(false).CheckError();
                started = true;
            }

            (device, manager) = TryCreateDeviceManager();
            IsHardwareAccelerated = manager is not null;
            if (device is not null)
            {
                _device = device;
                _context = device.ImmediateContext;
                _multithread = device.QueryInterface<ID3D11Multithread>();
            }
            reader = CreateReader(manager);
            ConfigureOutput(reader);

            Loop(reader);
        }
        catch (Exception ex) when (!_stop.IsCancellationRequested)
        {
            _onError?.Invoke(ex);
        }
        catch (Exception)
        {
            // 停止过程中的异常忽略。
        }
        finally
        {
            reader?.Dispose();
            _staging?.Dispose();
            _multithread?.Dispose();
            _context?.Dispose();
            manager?.Dispose();
            device?.Dispose();
            if (started)
            {
                lock (StartupLock)
                {
                    if (--_startupCount == 0) MediaFactory.MFShutdown();
                }
            }
        }
    }

    /// <summary>D3D11 视频设备 + DXGI 设备管理器，让解码与缩放走显卡；失败返回 null 时退回软件解码。</summary>
    private static (ID3D11Device?, IMFDXGIDeviceManager?) TryCreateDeviceManager()
    {
        ID3D11Device? device = null;
        try
        {
            FeatureLevel[] levels = [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0];
            D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware,
                DeviceCreationFlags.VideoSupport | DeviceCreationFlags.BgraSupport, levels, out device).CheckError();
            using (ID3D11Multithread multithread = device!.QueryInterface<ID3D11Multithread>())
            {
                multithread.SetMultithreadProtected(true);
            }
            IMFDXGIDeviceManager manager = MediaFactory.MFCreateDXGIDeviceManager();
            manager.ResetDevice(device).CheckError();
            return (device, manager);
        }
        catch (Exception)
        {
            device?.Dispose();
            return (null, null);
        }
    }

    private IMFSourceReader CreateReader(IMFDXGIDeviceManager? manager)
    {
        using IMFAttributes attributes = MediaFactory.MFCreateAttributes(4);
        attributes.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
        if (manager is not null)
        {
            attributes.Set(SourceReaderAttributeKeys.D3DManager, manager);
            attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, true);
        }
        // MF_LOW_LATENCY：解码器少缓存几帧。4K 硬件解码实测专用工作集从约 +205MB 降到约 +128MB，帧率不受影响。
        attributes.Set(LowLatency, true);
        IMFSourceReader reader = MediaFactory.MFCreateSourceReaderFromURL(_path, attributes);
        // 只要视频流，不解音频。
        reader.SetStreamSelection(SourceReaderIndex.AllStreams, false);
        reader.SetStreamSelection(SourceReaderIndex.FirstVideoStream, true);

        using IMFMediaType native = reader.GetNativeMediaType(SourceReaderIndex.FirstVideoStream, 0);
        ulong size = native.GetUInt64(MediaTypeAttributeKeys.FrameSize);
        SourceWidth = (int)(size >> 32);
        SourceHeight = (int)(size & 0xFFFFFFFF);
        return reader;
    }

    private int _outWidth, _outHeight;

    /// <summary>输出 RGB32：按「盖住请求区域」缩放（UniformToFill 所需的最小尺寸），保持宽高比，不放大。</summary>
    private void ConfigureOutput(IMFSourceReader reader)
    {
        _sizeChanged = false;
        double scale = Math.Min(1, Math.Max(Volatile.Read(ref _requestedWidth) / (double)SourceWidth,
            Volatile.Read(ref _requestedHeight) / (double)SourceHeight));
        int width = Math.Min(Even((int)Math.Ceiling(SourceWidth * scale)), Even(SourceWidth));
        int height = Math.Min(Even((int)Math.Ceiling(SourceHeight * scale)), Even(SourceHeight));
        if (width == _outWidth && height == _outHeight) return;
        using IMFMediaType type = MediaFactory.MFCreateMediaType();
        type.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        type.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
        type.Set(MediaTypeAttributeKeys.FrameSize, ((ulong)(uint)width << 32) | (uint)height);
        reader.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, type);
        _outWidth = width;
        _outHeight = height;
    }

    private void Loop(IMFSourceReader reader)
    {
        var clock = Stopwatch.StartNew();
        long frameTicks = TimeSpan.TicksPerSecond / MaxFps;     // 100ns 单位
        long baseClock = 0;                                      // 本轮开始时的时钟（100ns）
        long baseSample = -1;                                    // 本轮第一帧的时间戳
        long nextPresent = 0;                                    // 下一次允许输出的视频时间（限帧）

        while (!_stop.IsCancellationRequested)
        {
            if (!_running.IsSet)
            {
                long pausedAt = clock.Elapsed.Ticks;
                _running.Wait(_stop.Token);
                // 恢复后时间轴顺延，不追赶暂停期间的帧。
                baseClock += clock.Elapsed.Ticks - pausedAt;
            }
            if (_sizeChanged) ConfigureOutput(reader);

            using IMFSample? sample = reader.ReadSample(SourceReaderIndex.FirstVideoStream, SourceReaderControlFlag.None,
                out _, out SourceReaderFlag flags, out long timestamp);
            if ((flags & SourceReaderFlag.Error) != 0) throw new InvalidOperationException("视频解码出错");
            if ((flags & SourceReaderFlag.EndOfStream) != 0)
            {
                reader.SetCurrentPosition(0);
                baseSample = -1;
                nextPresent = 0;
                continue;
            }
            if (sample is null) continue;

            if (baseSample < 0)
            {
                baseSample = timestamp;
                baseClock = clock.Elapsed.Ticks;
            }
            long videoTime = timestamp - baseSample;
            // 限帧：源帧率高于 MaxFps 时丢弃间隔不足的帧。
            if (videoTime < nextPresent - frameTicks / 4) continue;
            nextPresent = Math.Max(nextPresent + frameTicks, videoTime);

            long wait = baseClock + videoTime - clock.Elapsed.Ticks;
            if (wait > TimeSpan.TicksPerMillisecond)
            {
                if (_stop.Token.WaitHandle.WaitOne(TimeSpan.FromTicks(Math.Min(wait, TimeSpan.TicksPerSecond)))) break;
            }
            else if (wait < -frameTicks * 3)
            {
                // 落后太多（如系统卡顿）：丢帧并把时间轴对齐到当前。
                baseClock = clock.Elapsed.Ticks - videoTime;
                continue;
            }
            if (!_running.IsSet) continue;

            Deliver(sample);
        }
    }

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11Multithread? _multithread;
    private ID3D11Texture2D? _staging;

    /// <summary>
    /// 硬件解码时样本是显存纹理：自己复制到可读的暂存纹理再映射读取（比 Media Foundation 自带的锁定读回快一个数量级），
    /// 暂存纹理按尺寸复用。不是显存样本时返回 false，走通用路径。
    /// </summary>
    private bool TryDeliverGpu(IMFSample sample)
    {
        if (_device is null || _context is null || _multithread is null) return false;
        using IMFMediaBuffer buffer = sample.GetBufferByIndex(0);
        using IMFDXGIBuffer? dxgi = buffer.QueryInterfaceOrNull<IMFDXGIBuffer>();
        if (dxgi is null) return false;

        using var texture = new ID3D11Texture2D(dxgi.GetResource(typeof(ID3D11Texture2D).GUID));
        uint subresource = dxgi.SubresourceIndex;
        Texture2DDescription source = texture.Description;
        if (_staging is null || _staging.Description.Width != source.Width || _staging.Description.Height != source.Height
            || _staging.Description.Format != source.Format)
        {
            _staging?.Dispose();
            var desc = new Texture2DDescription
            {
                Width = source.Width,
                Height = source.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = source.Format,
                SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                CPUAccessFlags = CpuAccessFlags.Read,
            };
            _staging = _device.CreateTexture2D(desc);
        }

        int width = Math.Min(_outWidth, (int)source.Width);
        int height = Math.Min(_outHeight, (int)source.Height);
        _multithread.Enter();
        try
        {
            _context.CopySubresourceRegion(_staging, 0, 0, 0, 0, texture, subresource, null);
            _context.Map(_staging, 0, MapMode.Read, MapFlags.None, out MappedSubresource mapped).CheckError();
            try { _onFrame(mapped.DataPointer, (int)mapped.RowPitch, width, height); }
            finally { _context.Unmap(_staging, 0); }
        }
        finally
        {
            _multithread.Leave();
        }
        return true;
    }

    private void Deliver(IMFSample sample)
    {
        if (TryDeliverGpu(sample)) return;
        using IMFMediaBuffer buffer = sample.ConvertToContiguousBuffer();
        IMF2DBuffer? buffer2D = null;
        try
        {
            buffer2D = buffer.QueryInterfaceOrNull<IMF2DBuffer>();
            if (buffer2D is not null)
            {
                buffer2D.Lock2D(out IntPtr scan0, out int pitch);
                try { _onFrame(scan0, pitch, _outWidth, _outHeight); }
                finally { buffer2D.Unlock2D(); }
            }
            else
            {
                buffer.Lock(out IntPtr data, out _, out int length);
                try
                {
                    int pitch = _outWidth * 4;
                    if (length >= pitch * _outHeight) _onFrame(data, pitch, _outWidth, _outHeight);
                }
                finally { buffer.Unlock(); }
            }
        }
        finally
        {
            buffer2D?.Dispose();
        }
    }
}
