#pragma warning disable 1591
using System.Collections.Generic;

namespace Jellyfin.Plugin.NewslettersRedux.Shared.Entities;

public class ContentIdJson
{
    public ContentIdJson()
    {
        ItemId = string.Empty;
        PosterPath = string.Empty;
    }

    public string ItemId { get; set; }
    
    public string PosterPath { get; set; }

    public Dictionary<string, object?> GetReplaceDict()
    {
        var itemDict = new Dictionary<string, object?> { { "{ItemID}", ItemId } };

        return itemDict;        
    }
}