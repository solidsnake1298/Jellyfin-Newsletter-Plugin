#pragma warning disable 1591
using System.Collections.Generic;
using System.Globalization;
using SQLitePCL.pretty;

namespace Jellyfin.Plugin.Newsletters.Shared.Entities;

public class JsonFileObj
{
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
        ItemId = string.Empty;
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

    public string ItemId { get; set; }

    public string PosterPath { get; set; }

    public string Type { get; set; }

    public int Emailed { get; set; }

    public JsonFileObj ConvertToObj(IReadOnlyList<ResultSetValue> row)
    {
        var obj = new JsonFileObj()
        {
            Filename = row[0].ToString(),
            Title = row[1].ToString(),
            Album = row[2].ToString(),
            Season = int.Parse(row[3].ToString(), CultureInfo.CurrentCulture),
            Episode = int.Parse(row[4].ToString(), CultureInfo.CurrentCulture),
            Overview = row[5].ToString(),
            ItemId = row[6].ToString(),
            PosterPath = row[7].ToString(),
            Type = row[8].ToString(),
            Emailed = int.Parse(row[9].ToString(), CultureInfo.CurrentCulture)
        };

        return obj;
    }

    public Dictionary<string, object?> GetReplaceDict()
    {
        Dictionary<string, object?> itemDict = new Dictionary<string, object?>
        {
            { "{Filename}", Filename },
            { "{Title}", Title },
            { "{Album}", Album },
            { "{Season}", Season },
            { "{Episode}", Episode },
            { "{Overview}", Overview },
            { "{ItemID}", ItemId },
            { "{PosterPath}", PosterPath },
            { "{Type}", Type },
            { "{ImageURL}", "cid:" + ItemId }
        };

        return itemDict;        
    }
}