#pragma warning disable 1591, SYSLIB0014, CA1002, CS0162
using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Jellyfin.Plugin.NewslettersRedux.Scanner;

public static class PosterImageHandler
{
    // Global Vars
    private const SKEncodedImageFormat DefaultImageFormat = SKEncodedImageFormat.Png;
    
    public static Stream ResizeImage(string imgPath)
    {
        var streamImage = SKImage.FromEncodedData(imgPath);
        using var skImage = SKBitmap.FromImage(streamImage);
        var extension = Path.GetExtension(imgPath);
        var width = skImage.Width;

        // Creates scale factor for height to maintain aspect ratio for 200px width
        var scaleFactor = 200.0 / width;
            
        var newHeight = (int)(skImage.Height * scaleFactor);
        // if scaleFactor is 1, skip resizing
        if (scaleFactor is 1)
        {
            using var image = SKImage.FromBitmap(skImage);
            using var encodedImage = image.Encode(GetSkiaSharpImageFormatFromExtension(extension), 50);
            var stream = new MemoryStream();
            encodedImage.SaveTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
        else
        {
            // TODO: Figure out higher quality scaling options equivalent to SkFilterQuality High.
            var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            using var scaledBitmap = skImage.Resize(new SKSizeI(200, newHeight), samplingOptions);
            using var image = SKImage.FromBitmap(scaledBitmap);
            using var encodedImage = image.Encode(GetSkiaSharpImageFormatFromExtension(extension), 50);
            var stream = new MemoryStream();
            encodedImage.SaveTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }

    public static Stream DrawBlackSquare()
    {
        var info = new SKImageInfo(200, 200, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        using var paint = new SKPaint();
        paint.Color = SKColors.Black;
        var square = new SKRect(0, 0, 0 + 200, 0 + 200);
        canvas.DrawRect(square, paint);
        var streamImage = surface.Snapshot();

        using var encodedImage = streamImage.Encode(GetSkiaSharpImageFormatFromExtension(".png"), 50);
        var stream = new MemoryStream();
        encodedImage.SaveTo(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private static SKEncodedImageFormat GetSkiaSharpImageFormatFromExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        Dictionary<string, SKEncodedImageFormat> skiaSharpImageFormatMapping = new(StringComparer.InvariantCultureIgnoreCase)
        {
            { ".png", SKEncodedImageFormat.Png },
            { ".jpg", SKEncodedImageFormat.Jpeg },
            { ".jpeg", SKEncodedImageFormat.Jpeg },
            { ".jpe", SKEncodedImageFormat.Jpeg }
        };

        return skiaSharpImageFormatMapping.GetValueOrDefault(extension, DefaultImageFormat);
    }
}