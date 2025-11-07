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
        ImageURL = string.Empty;
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

    public string ImageURL { get; set; }

    public string ItemID { get; set; }

    public string PosterPath { get; set; }

    public string Type { get; set; }

    public int Emailed { get; set; }

    public JsonFileObj ConvertToObj(IReadOnlyList<ResultSetValue> row)
    {
        // Filename = string.Empty; 0
        // Title = string.Empty; 1
        // Album = string.Empty; 2
        // Season = 0; 3
        // Episode = 0; 4
        // Overview = string.Empty; 5
        // ImageURL = string.Empty; 6
        // ItemID = string.Empty; 7
        // PosterPath = string.Empty; 8
        // Album = string.Empty; 9
        // Emailed = 0; 10

        logger = new Logger();
        JsonFileObj obj = new JsonFileObj()
        {
            Filename = row[0].ToString(),
            Title = row[1].ToString(),
            Album = row[2].ToString(),
            Season = int.Parse(row[3].ToString(), CultureInfo.CurrentCulture),
            Episode = int.Parse(row[4].ToString(), CultureInfo.CurrentCulture),
            Overview = row[5].ToString(),
            ImageURL = row[6].ToString(),
            ItemID = row[7].ToString(),
            PosterPath = row[8].ToString(),
            Type = row[9].ToString(),
            Emailed = int.Parse(row[10].ToString(), CultureInfo.CurrentCulture)
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
        item_dict.Add("{ImageURL}", this.ImageURL);
        item_dict.Add("{ItemID}", this.ItemID);
        item_dict.Add("{PosterPath}", this.PosterPath);
        item_dict.Add("{Type}", this.Type);

        return item_dict;        
    }
}