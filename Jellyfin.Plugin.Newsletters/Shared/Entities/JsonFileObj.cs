#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jellyfin.Plugin.Newsletters.LOGGER;
using SQLitePCL;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;

public class JsonFileObj
{
    private Logger? logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileObj"/> class.
    /// </summary>
    public JsonFileObj()
    {
        Filename = string.Empty;
        Title = string.Empty;
        Album = string.Empty;
        Season = 0;
        Episode = 0;
        Overview = string.Empty;
        ItemID = string.Empty;
        PosterPath = string.Empty;
        Type = string.Empty;
        Emailed = 0;
    }

    public string Filename { get; set; }

    public string Title { get; set; }

    public string Album { get; set; }

    public int Season { get; set; }

    public int Episode { get; set; }

    public string Overview { get; set; }

    public string ItemID { get; set; }

    public string PosterPath { get; set; }

    public string Type { get; set; }

    public int Emailed { get; set; }

    public JsonFileObj ConvertToObj(IReadOnlyList<ResultSetValue> row)
    {
        logger = new Logger();
        JsonFileObj obj = new JsonFileObj()
        {
            Filename = row[0].ToString(),
            Title = row[1].ToString(),
            Album = row[2].ToString(),
            Season = int.Parse(row[3].ToString(), CultureInfo.CurrentCulture),
            Episode = int.Parse(row[4].ToString(), CultureInfo.CurrentCulture),
            Overview = row[5].ToString(),
            ItemID = row[6].ToString(),
            PosterPath = row[7].ToString(),
            Type = row[8].ToString(),
            Emailed = int.Parse(row[9].ToString(), CultureInfo.CurrentCulture)
        };

        return obj;
    }

    public Dictionary<string, object?> GetReplaceDict()
    {
        Dictionary<string, object?> item_dict = new Dictionary<string, object?>();
        item_dict.Add("{Filename}", this.Filename);
        item_dict.Add("{Title}", this.Title);
        item_dict.Add("{Album}", this.Album);
        item_dict.Add("{Season}", this.Season);
        item_dict.Add("{Episode}", this.Episode);
        item_dict.Add("{Overview}", this.Overview);
        item_dict.Add("{ItemID}", this.ItemID);
        item_dict.Add("{PosterPath}", this.PosterPath);
        item_dict.Add("{Type}", this.Type);
        item_dict.Add("{ImageURL}", "cid:" + this.ItemID);

        return item_dict;        
    }
}