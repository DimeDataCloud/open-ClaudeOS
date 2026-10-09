using ClaudeOS.Shell.Interop;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Takes a picture of the whole screen as the compositor shows it (acrylic, rounded corners and
/// all) and saves it as a PNG. Only the self-test uses it, so a CI run can show what the shell
/// looks like and not just that it ran.
/// </summary>
internal static unsafe class ScreenShot
{
    public static async Task SaveAsync(string path)
    {
        var width = Native.GetSystemMetrics(0);
        var height = Native.GetSystemMetrics(1);
        var screen = Native.GetDC(0);
        var memory = Native.CreateCompatibleDC(screen);
        var bitmap = Native.CreateCompatibleBitmap(screen, width, height);
        var previous = Native.SelectObject(memory, bitmap);
        try
        {
            Native.BitBlt(memory, 0, 0, width, height, screen, 0, 0, Native.SRCCOPY | Native.CAPTUREBLT);
            var pixels = new byte[width * height * 4];
            var info = new Native.BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(Native.BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height, // top-down
                biPlanes = 1,
                biBitCount = 32,
            };
            fixed (byte* p = pixels)
            {
                Native.GetDIBits(memory, bitmap, 0, (uint)height, p, &info, 0);
            }

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, pixels);
            await encoder.FlushAsync();
            stream.Seek(0);
            var bytes = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes);
        }
        finally
        {
            Native.SelectObject(memory, previous);
            Native.DeleteObject(bitmap);
            Native.DeleteDC(memory);
            Native.ReleaseDC(0, screen);
        }
    }
}
