#pragma warning disable 1591, SYSLIB0014, CA1002, CS0162, SA1005 // remove SA1005 for cleanup
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.Shared.Database;
using Jellyfin.Plugin.Newsletters.Shared.Entities;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.Newsletters.Emails;

public class HtmlBuilder
{
    // Global Vars
    // Readonly
    private readonly PluginConfiguration config;
    private readonly Logger logger;
    private readonly SqLiteDatabase db;
    private readonly JsonFileObj jsonHelper;
    private readonly List<string> contentIdList = new();
    // Non-readonly
    private string emailBody;

    public HtmlBuilder()
    {
        logger = new Logger();
        jsonHelper = new JsonFileObj();
        db = new SqLiteDatabase();
        config = Plugin.Instance!.Configuration;
        emailBody = config.Body;
    }

    private string TemplateReplace(string htmlObj, string replaceKey, object replaceValue)
    {
        logger.Debug("Replacing {} params:\n " + htmlObj);
        if (replaceKey == "{RunTime}" && (int)replaceValue == 0)
        {
            logger.Debug($"{replaceKey} == {replaceValue}");
            logger.Debug("Skipping replace..");
            return htmlObj;
        }

        logger.Debug($"Replace Value {replaceKey} with " + replaceValue);

        htmlObj = htmlObj.Replace(replaceKey, replaceValue.ToString(), StringComparison.Ordinal);
        
        logger.Debug("New HTML OBJ: \n" + htmlObj);
        return htmlObj;
    }
    
    public string GetDefaultHtmlBody()
    {
        emailBody = config.Body;
        return emailBody;
    }

    public string BuildDataHtmlStringFromNewsletterData()
    {
        var completed = new List<string>();
        var builtHtmlString = string.Empty;
        
        // Pull data from NewsletterData table
        try
        {
            db.CreateConnection();

            foreach (var row in db.Query("SELECT * FROM NewsletterData WHERE Emailed = 0 AND (Type = 'Series' OR Type = 'Movie');"))
            {
                    var contentId = new ContentIdJson();
                    var item = jsonHelper.ConvertToObj(row);
                    // scan through all items and get all Season numbers and Episodes
                    if (completed.Contains(item.Title))
                    {
                        continue;
                    }

                    var seaEpsHtml = string.Empty;
                    if (item.Type == "Series")
                    {
                        // for series only
                        var parsedInfoList = ParseSeriesInfo(item);
                        seaEpsHtml += GetSeasonEpisodeHtml(parsedInfoList);
                    }

                    var tmpEntry = config.Entry;

                    contentId.PosterPath = item.PosterPath;
                    contentId.ItemId = item.ItemId;

                    foreach (var ele in item.GetReplaceDict())
                    {
                        if (ele.Value is not null)
                        {
                            tmpEntry = TemplateReplace(tmpEntry, ele.Key, ele.Value);
                        }
                    }

                    builtHtmlString += tmpEntry.Replace("{TitleInfo}", seaEpsHtml, StringComparison.Ordinal)
                                                .Replace("{ImageURL}", "cid:<" + item.ItemId + ">", StringComparison.Ordinal);

                    contentIdList.Add(JsonConvert.SerializeObject(contentId));
                    completed.Add(item.Title);
            }

            foreach (var row in db.Query("SELECT * FROM NewsletterData WHERE Emailed = 0 AND Type = 'Album';"))
            {
                var contentId = new ContentIdJson();
                var item = jsonHelper.ConvertToObj(row);
                if (completed.Contains(item.Title))
                {
                    continue;
                }

                var albumsHtml = string.Empty;
                if (item.Type == "Album")
                {
                    var parsedInfoList = ParseMusicInfo(item);
                    albumsHtml += GetSeasonEpisodeHtml(parsedInfoList);
                }

                var tmpEntry = config.Entry;
                    
                contentId.PosterPath = item.PosterPath;
                contentId.ItemId = item.ItemId;

                foreach (var ele in item.GetReplaceDict())
                {
                    if (ele.Value is not null)
                    {
                        tmpEntry = TemplateReplace(tmpEntry, ele.Key, ele.Value);
                    }
                }

                builtHtmlString += tmpEntry.Replace("{TitleInfo}", albumsHtml, StringComparison.Ordinal)
                                            .Replace("{ImageURL}", "cid:<" + item.ItemId + ">", StringComparison.Ordinal);
                    
                contentIdList.Add(JsonConvert.SerializeObject(contentId));
                completed.Add(item.Title);
            }
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            db.CloseConnection();
            logger.Debug("Finished building email!");
        }

        return builtHtmlString;
    }

    public List<string> BuildContentId()
    {
        return contentIdList;
    }

    private string GetSeasonEpisodeHtml(List<NlDetailsJson> list)
    {
        var html = string.Empty;
        foreach (var obj in list)
        {
            logger.Debug("SNIPPET OBJ: " + JsonConvert.SerializeObject(obj));
            switch (obj.Type)
            {
                case "Series":
                    html += "Season: " + obj.Season + " - Eps. " + obj.EpisodeRange + "<br>";
                    break;
                case "Album":
                    html += "Album: " + obj.Album + "<br>";
                    break;
            }
        }

        return html;
    }

    private List<NlDetailsJson> ParseSeriesInfo(JsonFileObj currObj)
    {
        var compiledList = new List<NlDetailsJson>();
        var finalList = new List<NlDetailsJson>();

        // Creates list of episodes + seasons for a series to be added to the newsletter.  Or individual movies.
        foreach (var row in db.Query($"SELECT * FROM NewsletterData WHERE Emailed = 0 AND Title='{currObj.Title}';"))
        {
            var helper = new JsonFileObj();
            var itemObj = helper.ConvertToObj(row);

            var tempVar = new NlDetailsJson()
            {
                Title = itemObj.Title,
                Season = itemObj.Season,
                Episode = itemObj.Episode,
                EndEpisode = itemObj.EndEpisode,
                Type = itemObj.Type
            };

            logger.Debug("tempVar.Season: " + tempVar.Season + " : tempVar.Episode: " + tempVar.Episode);
            compiledList.Add(tempVar);
        }

        var tempEpsList = new List<int>();
        var currSeriesDetailsObj = new NlDetailsJson();

        // Parse episode/season/movie list
        var currSeason = -1;
        var newSeason = true;
        var listLen = compiledList.Count;
        var count = 1;
        foreach (var item in SortListBySeason(SortListByEpisode(compiledList)))
        {
            logger.Debug("After Sort in foreach: Season::" + item.Season + "; Episode::" + item.Episode + "; EndEpisode::" + item.EndEpisode);
            logger.Debug("Count/list_len: " + count + "/" + listLen);
            currSeriesDetailsObj.Title = item.Title;

            // Inserts attributes from previous loops into final JSON object
            NlDetailsJson CopyJsonFromExisting(NlDetailsJson obj)
            {
                var newJson = new NlDetailsJson
                {
                    Season = obj.Season,
                    EpisodeRange = obj.EpisodeRange,
                    Type = obj.Type
                };
                return newJson;
            }

            logger.Debug("CurrItem Season/Episode number: " + item.Season + "/" + item.Episode);
            if (newSeason)
            {
                AddNewSeason();
            }
            else if (currSeason == item.Season) // && (count < list_len))
            {
                AddCurrentSeason();
            }
            else if (count < listLen)
            {
                EndOfSeason();
                AddNewSeason();
            }
            else if (count == listLen)
            {
                EndOfSeason();
            }
            else
            {
                EndOfSeason();
            }

            if (count == listLen)
            {
                EndOfSeason();
            }

            count++;
            continue;

            void AddCurrentSeason()
            {
                // logger.Debug("AddCurrentSeason()");
                logger.Debug("Seasons Match " + currSeason + "::" + item.Season);
                if (item.EndEpisode > 0)
                {
                    logger.Debug("Multi-episode file: Episodes " + item.Episode + " to " + item.EndEpisode);
                    var epCount = 0;
                    var epDiff = item.EndEpisode - item.Episode;
                    while (epCount <= epDiff)
                    {
                        var curEp = item.Episode + epCount;
                        tempEpsList.Add(curEp);
                        epCount++;
                    }
                }
                else
                {
                    tempEpsList.Add(item.Episode);
                }
            }

            void EndOfSeason()
            {
                // process season, then increment
                logger.Debug("EndOfSeason()");
                logger.Debug($"tempEpsList Size: {tempEpsList.Count}");
                // Sorts and dedupes episode number list to handle seasons with multiple versions of an episode
                tempEpsList = tempEpsList.Distinct().OrderBy(x => x).ToList();
                if (tempEpsList.Count != 0)
                {
                    logger.Debug("tempEpsList is populated");
                    tempEpsList.Sort();
                    if (IsIncremental(tempEpsList))
                    {
                        // If list is a single episode, do not treat as range
                        if (tempEpsList.Count == 1)
                        {
                            currSeriesDetailsObj.EpisodeRange = $"{tempEpsList.First()}";
                        }
                        else
                        {
                            currSeriesDetailsObj.EpisodeRange = tempEpsList.First() + " - " + tempEpsList.Last();
                        }
                    }
                    else if (tempEpsList.First() == tempEpsList.Last())
                    {
                        currSeriesDetailsObj.EpisodeRange = tempEpsList.First().ToString(System.Globalization.CultureInfo.CurrentCulture);
                    }
                    else
                    {
                        var epList = string.Empty;
                        int firstRangeEp, prevEp;
                        firstRangeEp = prevEp = -1;

                        bool IsNext(int prev, int curr)
                        {
                            logger.Debug("Checking Prev and Curr..");
                            logger.Debug($"prev: {prev} :: curr: {curr}");
                            logger.Debug(prev + 1);
                            return curr == prev + 1;
                        }

                        void ProcessEpString(int firstEp, int nextEp)
                        {
                            if (firstEp == nextEp)
                            {
                                epList += firstEp + ",";
                            }
                            else
                            {
                                epList += firstEp + "-" + nextEp + ",";
                            }
                        }

                        foreach (var ep in tempEpsList)
                        {
                            logger.Debug("-------------------");
                            logger.Debug($"FOREACH firstRangeEp :: prevEp :: ep = {firstRangeEp} :: {prevEp} :: {ep} ");
                            logger.Debug(ep == tempEpsList.Last());
                            // if first passthrough
                            if (firstRangeEp == -1)
                            {
                                logger.Debug("First pass of episode list");
                                firstRangeEp = prevEp = ep;
                                continue;
                            }

                            // If incremental
                            if (IsNext(prevEp, ep) && (ep != tempEpsList.Last()))
                            {
                                logger.Debug("Is Next and Isn't last");
                                prevEp = ep;
                            }
                            else if (IsNext(prevEp, ep) && (ep == tempEpsList.Last()))
                            {
                                logger.Debug("Is Next and Is last");
                                prevEp = ep;
                                ProcessEpString(firstRangeEp, prevEp);
                            }
                            else if (!IsNext(prevEp, ep) && (ep == tempEpsList.Last()))
                            {
                                logger.Debug("Isn't Next and Is last");
                                // process previous
                                ProcessEpString(firstRangeEp, prevEp);
                                // process last episode
                                epList += ep;
                            }
                            else
                            {
                                logger.Debug("Isn't Next and Isn't last");
                                ProcessEpString(firstRangeEp, prevEp);
                                firstRangeEp = prevEp = ep;
                            }
                        }

                        // better numbering here
                        logger.Debug($"epList: {epList}");
                        currSeriesDetailsObj.EpisodeRange = epList.TrimEnd(',');
                    }

                    logger.Debug("Adding to finalListObj: " + JsonConvert.SerializeObject(currSeriesDetailsObj));
                    // finalList.Add(currSeriesDetailsObj);
                    finalList.Add(CopyJsonFromExisting(currSeriesDetailsObj));

                    // increment season
                    currSeriesDetailsObj.Season = currSeason = item.Season;
                    currSeriesDetailsObj.EpisodeRange = string.Empty;

                    // currSeason = item.Season;
                    tempEpsList.Clear();
                    newSeason = true;
                }
            }

            void AddNewSeason()
            {
                logger.Debug("AddNewSeason()");
                currSeriesDetailsObj.Season = currSeason = item.Season;
                currSeriesDetailsObj.Type = item.Type;
                newSeason = false;
                if (item.EndEpisode > 0)
                {
                    logger.Debug("Multi-episode file: Episodes " + item.Episode + " to " + item.EndEpisode);
                    var epCount = 0;
                    var epDiff = item.EndEpisode - item.Episode;
                    while (epCount <= epDiff)
                    {
                        var curEp = item.Episode + epCount;
                        tempEpsList.Add(curEp);
                        epCount++;
                    }
                }
                else
                {
                    tempEpsList.Add(item.Episode);
                }
            }
        }

        logger.Debug("FinalList Length: " + finalList.Count);

        // Prevents entering foreach loop when debug logging is not enabled
        if (config.DebugMode)
        {
            foreach (var item in finalList)
            {
                logger.Debug("FinalListObjs: " + JsonConvert.SerializeObject(item));
            }
        }

        return finalList;
    }

    private List<NlDetailsJson> ParseMusicInfo(JsonFileObj currObj)
    {
        var compiledList = new List<NlDetailsJson>();
        var finalList = new List<NlDetailsJson>();

        // Creates list of albums to be added to the newsletter
        foreach (var row in db.Query($"SELECT * FROM NewsletterData WHERE Emailed = 0 AND Title='{currObj.Title}';"))
        {
            var helper = new JsonFileObj();
            var itemObj = helper.ConvertToObj(row);

            var tempVar = new NlDetailsJson()
            {
                Title = itemObj.Title,
                Album = itemObj.Album,
                Type = itemObj.Type
            };

            logger.Debug("tempVar.Album: " + tempVar.Album);
            compiledList.Add(tempVar);
        }

        var currAlbumDetailsObj = new NlDetailsJson();

        // Parses album list
        var listLen = compiledList.Count;
        var count = 1;
        foreach (var item in compiledList)
        {
            logger.Debug("After Sort in foreach: Album::" + item.Album);
            logger.Debug("Count/list_len: " + count + "/" + listLen);
            currAlbumDetailsObj.Title = item.Title;

            // Inserts album attributes from previous loops into JSON object
            NlDetailsJson CopyJsonFromExisting(NlDetailsJson obj)
            {
                var newJson = new NlDetailsJson
                {
                    Album = obj.Album,
                    Type = obj.Type
                };
                return newJson;
            }

            logger.Debug("CurrItem Album: " + item.Album);
            if (count <= listLen)
            {
                AddNewAlbum();
            }

            count++;
            continue;

            // Inserts album attributes from current loop into JSON object
            void AddNewAlbum()
            {
                currAlbumDetailsObj.Album = item.Album;
                currAlbumDetailsObj.Type = item.Type;
                finalList.Add(CopyJsonFromExisting(currAlbumDetailsObj));
            }
        }

        logger.Debug("FinalList Length: " + finalList.Count);

        // Prevents entering foreach loop when debug logging is not enabled
        if (config.DebugMode)
        {
            foreach (var item in finalList)
            {
                logger.Debug("FinalListObjs: " + JsonConvert.SerializeObject(item));
            }
        }

        return finalList;
    }

    private static bool IsIncremental(List<int> values)
    {
        return values.Skip(1).Select((v, i) => v == (values[i] + 1)).All(v => v);
    }

    private static List<NlDetailsJson> SortListBySeason(List<NlDetailsJson> list)
    {
        return list.OrderBy(x => x.Season).ToList();
    }

    private static List<NlDetailsJson> SortListByEpisode(List<NlDetailsJson> list)
    {
        return list.OrderBy(x => x.Episode).ToList();
    }

    public string ReplaceBodyWithBuiltString(string body, string nlData)
    {
        return body.Replace("{EntryData}", nlData, StringComparison.Ordinal);
    }

    public void CleanUp()
    {
        // Updates Emailed column to 1
        db.CreateConnection();
        db.ExecuteSql("UPDATE NewsletterData SET Emailed = 1 WHERE Emailed = 0 OR Emailed IS NULL;");
        db.CloseConnection();
    }
}