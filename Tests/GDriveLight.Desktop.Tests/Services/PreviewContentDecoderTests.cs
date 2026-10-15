using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GDriveLight.Desktop.Services.Preview;

namespace GDriveLight.Desktop.Tests.Services;

public sealed class PreviewContentDecoderTests
{
    [Theory]
    [InlineData("photo.JPEG", FilePreviewKind.Image)]
    [InlineData("image.jpg", FilePreviewKind.Image)]
    [InlineData("image.png", FilePreviewKind.Image)]
    [InlineData("image.gif", FilePreviewKind.Image)]
    [InlineData("image.tiff", FilePreviewKind.Image)]
    [InlineData("Example.CS", FilePreviewKind.Text)]
    [InlineData("page.html", FilePreviewKind.Text)]
    [InlineData("image.svg", FilePreviewKind.Text)]
    [InlineData("Dockerfile", FilePreviewKind.Text)]
    [InlineData(".gitignore", FilePreviewKind.Text)]
    [InlineData("file.json", FilePreviewKind.Text)]
    [InlineData("file.pdf", FilePreviewKind.Unsupported)]
    [InlineData("file.docx", FilePreviewKind.Unsupported)]
    [InlineData("file.exe", FilePreviewKind.Unsupported)]
    public void NativePreviewTypesAreExplicit(string name, FilePreviewKind kind) =>
        Assert.Equal(kind, FilePreviewPolicy.GetKind(name));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceCodeAndUnicodeRemainLiteralText(bool utf16)
    {
        const string source = "// Привіт\r\nclass Example { string value = \"<script>alert(1)</script>\"; }";
        var encoding = utf16 ? Encoding.Unicode : new UTF8Encoding(false);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(source)).ToArray();
        Assert.Equal(source, PreviewContentDecoder.DecodeText(bytes));
        Assert.Equal(string.Empty, PreviewContentDecoder.DecodeText([]));
    }

    [Fact]
    public void BinaryDataAndInvalidUtf8AreRejected()
    {
        Assert.Throws<PreviewUnavailableException>(() => PreviewContentDecoder.DecodeText([0x4D, 0x5A, 0, 0]));
        Assert.Throws<DecoderFallbackException>(() => PreviewContentDecoder.DecodeText([0xC3, 0x28]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JpegAndPngDecodeIntoFrozenBitmapsWithoutKeepingTheStreamOpen(bool jpeg)
    {
        var bytes = CreateImage(jpeg);
        var image = await Task.Run(() => PreviewContentDecoder.DecodeImage(bytes));
        Assert.True(image.IsFrozen);
        Assert.Equal(2, image.PixelWidth);
        Assert.Equal(1, image.PixelHeight);
        image.CopyPixels(new byte[16], 8, 0);
    }

    internal static byte[] CreateImage(bool jpeg)
    {
        var bitmap = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Bgr24, null,
            new byte[] { 0, 0, 255, 0, 255, 0 }, 6);
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder() : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
