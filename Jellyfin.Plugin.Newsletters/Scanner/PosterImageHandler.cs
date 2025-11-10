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

    // Non-readonly
    private List<JsonFileObj> archiveSeriesList;
    // private List<string> fileList;

    public PosterImageHandler()
    {
        logger = new Logger();
        db = new SQLiteDatabase();
        config = Plugin.Instance!.Configuration;
        jsonHelper = new JsonFileObj();

        archiveSeriesList = new List<JsonFileObj>();
    }

    public static string ConvertImageToBase64(string imgPath)
    {
        var streamImage = SKImage.FromEncodedData(imgPath);
        using (var skImage = SKBitmap.FromImage(streamImage))
        {
            string extension = Path.GetExtension(imgPath);
            string base64MimeType = GetMimeTypeFromExtension(extension);
            int width = skImage.Width;
            int targetWidth = 200;
            double scaleFactor = targetWidth / width;
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
                        string base64Image = Convert.ToBase64String(stream.ToArray());
                        return $"data:{base64MimeType};base64, {base64Image}";
                    }
                }
            }
            else
            {
                using (var scaledBitmap = skImage.Resize(new SKSizeI(targetWidth, newHeight), SKFilterQuality.Low))
                {
                    using (var image = SKImage.FromBitmap(scaledBitmap))
                    {
                        using (var encodedImage = image.Encode(GetSkiaSharpImageFormatFromExtension(extension), 50))
                        {
                            var stream = new MemoryStream();
                            encodedImage.SaveTo(stream);
                            stream.Seek(0, SeekOrigin.Begin);
                            string base64Image = Convert.ToBase64String(stream.ToArray());
                            return $"data:{base64MimeType};base64, {base64Image}";
                        }
                    }
                }
            }
        }
    }

    public static string GetMimeTypeFromExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return MimeTypeMapping.TryGetValue(extension, out string mimeType) ? mimeType : DefaultMimeType;
    }

    private static SKEncodedImageFormat GetSkiaSharpImageFormatFromExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return _skiaSharpImageFormatMapping.TryGetValue(extension, out SKEncodedImageFormat imageFormat) ? imageFormat : DefaultImageFormat;
    }

    private static readonly Dictionary<string, string> MimeTypeMapping = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { ".jpe", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".jpg", "image/jpeg" },
        { ".png", "image/png" }
    };

    private static readonly Dictionary<string, SKEncodedImageFormat> _skiaSharpImageFormatMapping = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { ".png", SKEncodedImageFormat.Png },
        { ".jpg", SKEncodedImageFormat.Jpeg },
        { ".jpeg", SKEncodedImageFormat.Jpeg },
        { ".jpe", SKEncodedImageFormat.Jpeg }
    };
}