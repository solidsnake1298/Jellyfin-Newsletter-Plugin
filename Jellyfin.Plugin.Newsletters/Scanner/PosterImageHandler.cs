#pragma warning disable 1591, SYSLIB0014, CA1002, CS0162
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.LOGGER;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using Jellyfin.Plugin.Newsletters.Scripts.SCRAPER;
using Jellyfin.Plugin.Newsletters.Shared.DATA;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;

// using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;

public class PosterImageHandler
{
    // Global Vars
    private const string DefaultMimeType = "application/octet-stream";
    private const SKEncodedImageFormat DefaultImageFormat = SKEncodedImageFormat.Png;
    
    // Readonly
    private readonly PluginConfiguration config;
    private Logger logger;
    private SQLiteDatabase db;
    private JsonFileObj jsonHelper;

    public PosterImageHandler()
    {
        logger = new Logger();
        db = new SQLiteDatabase();
        config = Plugin.Instance!.Configuration;
        jsonHelper = new JsonFileObj();
    }

    public static Stream ResizeImage(string imgPath)
    {
        var streamImage = SKImage.FromEncodedData(imgPath);
        using (var skImage = SKBitmap.FromImage(streamImage))
        {
            string extension = Path.GetExtension(imgPath);
            int width = skImage.Width;
            double scaleFactor = 200.0 / width;
            if (scaleFactor <= 0)
            {
                scaleFactor = 0.5;
            }
            
            int newHeight = (int)(skImage.Height * scaleFactor);

            if (scaleFactor is 1)
            {
                using (var image = SKImage.FromBitmap(skImage))
                {
                    using (var encodedImage = image.Encode(GetSkiaSharpImageFormatFromExtension(extension), 50))
                    {
                        var stream = new MemoryStream();
                        encodedImage.SaveTo(stream);
                        stream.Seek(0, SeekOrigin.Begin);
                        //string base64Image = Convert.ToBase64String(stream.ToArray());
                        return stream;
                    }
                }
            }
            else
            {
                SKSamplingOptions samplingOptions = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.Nearest);
                using (var scaledBitmap = skImage.Resize(new SKSizeI(200, newHeight), samplingOptions))
                {
                    using (var image = SKImage.FromBitmap(scaledBitmap))
                    {
                        using (var encodedImage = image.Encode(GetSkiaSharpImageFormatFromExtension(extension), 50))
                        {
                            var stream = new MemoryStream();
                            encodedImage.SaveTo(stream);
                            stream.Seek(0, SeekOrigin.Begin);
                            return stream;
                        }
                    }
                }
            }
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

        using (var encodedImage = streamImage.Encode(GetSkiaSharpImageFormatFromExtension(".png"), 50))
        {
            var stream = new MemoryStream();
            encodedImage.SaveTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }

    public static string GetMimeTypeFromExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        Dictionary<string, string> mimeTypeMapping = new Dictionary<string, string>
        {
            { ".jpe", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".jpg", "image/jpeg" },
            { ".png", "image/png" }
        };

        return mimeTypeMapping.TryGetValue(extension, out string mimeType) ? mimeType : DefaultMimeType;
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

        return skiaSharpImageFormatMapping.TryGetValue(extension, out SKEncodedImageFormat imageFormat) ? imageFormat : DefaultImageFormat;
    }
}