using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace GDriveLight.Desktop.Services.Preview;

public static class PreviewContentDecoder
{
    public static string DecodeText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        if (text.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t' and not '\f'))
            throw new PreviewUnavailableException("This file contains binary data and cannot be shown as text. Download it to view it.");
        return text;
    }

    public static BitmapSource DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 64_000_000)
            throw new PreviewUnavailableException("This image has too many pixels to preview. Download it to view the original.");

        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        // Keep the preview bitmap bounded even for large, highly compressed images.
        if (frame.PixelWidth >= frame.PixelHeight) image.DecodePixelWidth = Math.Min(frame.PixelWidth, 4096);
        else image.DecodePixelHeight = Math.Min(frame.PixelHeight, 4096);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
