#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.Newsletters.LOGGER;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;

public class ContentIdJson
{
    private Logger? logger;

    public ContentIdJson()
    {
        ItemID = string.Empty;
        MimeType = string.Empty;
        PosterPath = string.Empty;
    }

    public string ItemID { get; set; }
    
    public string MimeType { get; set; }
    
    public string PosterPath { get; set; }

    public ContentIdJson ConvertToObj(IReadOnlyList<ResultSetValue> row)
    {
        logger = new Logger();
        logger = new Logger();
        ContentIdJson obj = new ContentIdJson()
        {
            ItemID = row[0].ToString(),
            MimeType = row[1].ToString(),
            PosterPath = row[2].ToString()
        };

        return obj;
    }

    public Dictionary<string, object?> GetReplaceDict()
    {
        Dictionary<string, object?> item_dict = new Dictionary<string, object?>();
        item_dict.Add("{ItemID}", this.ItemID);
        item_dict.Add("{MimeType}", this.MimeType);

        return item_dict;        
    }
}