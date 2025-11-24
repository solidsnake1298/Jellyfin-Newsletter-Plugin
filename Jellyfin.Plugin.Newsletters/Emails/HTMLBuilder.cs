#pragma warning disable 1591, SYSLIB0014, CA1002, CS0162, SA1005 // remove SA1005 for cleanup
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.NLPLogger;
using Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;
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
// using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Newsletters.Emails.HTMLBuilder;

public class HtmlBuilder
{
    // Global Vars
    // Readonly
    private readonly PluginConfiguration config;
    private readonly string newslettersDir;
    private readonly string newsletterHTMLFile;

    private string emailBody;
    private Logger logger;
    private SqlLiteDatabase db;
    private JsonFileObj jsonHelper;
    private ContentIdJson contentIdHelper;
    private List<string> contentIdList = new List<string>();

    public HtmlBuilder()
    {
        logger = new Logger();
        jsonHelper = new JsonFileObj();
        contentIdHelper = new ContentIdJson();
        db = new SqlLiteDatabase();
        config = Plugin.Instance!.Configuration;
        emailBody = config.Body;

        newslettersDir = config.NewsletterDir;
        Directory.CreateDirectory(newslettersDir);

        // if no newsletter filename is saved or the file doesn't exist
        if (config.NewsletterFileName.Length == 0 || File.Exists(newslettersDir + config.NewsletterFileName))
        {
            // use date to create filename
            string currDate = DateTime.Today.ToString("yyyy/MM/dd h:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture);
            newsletterHTMLFile = newslettersDir + currDate + "_Newsletter.html";
        }
        else
        {
            newsletterHTMLFile = newslettersDir + config.NewsletterFileName;
        }
    }

    public string TemplateReplace(string htmlObj, string replaceKey, object replaceValue, bool finalPass = false)
    {
        logger.Debug("Replacing {} params:\n " + htmlObj);
        if (replaceValue is null)
        {
            logger.Debug($"Replace string is null.. Nothing to replace");
            return htmlObj;
        }

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

    public string BuildDataHtmlStringFromNewsletterData()
    {
        List<string> completed = new List<string>();
        string builtHTMLString = string.Empty;
        
        // Pull data from NewsletterData table
        try
        {
            db.CreateConnection();

            foreach (var row in db.Query("SELECT * FROM NewsletterData WHERE Emailed = 0 AND (Type = 'Series' OR Type = 'Movie');"))
            {
                if (row is not null)
                {
                    ContentIdJson contentID = new ContentIdJson();
                    JsonFileObj item = jsonHelper.ConvertToObj(row);
                    // scan through all items and get all Season numbers and Episodes
                    if (completed.Contains(item.Title))
                    {
                        continue;
                    }

                    string seaEpsHtml = string.Empty;
                    if (item.Type == "Series")
                    {
                        // for series only
                        List<NlDetailsJson> parsedInfoList = ParseSeriesInfo(item);
                        seaEpsHtml += GetSeasonEpisodeHTML(parsedInfoList);
                    }

                    var tmpEntry = config.Entry;

                    contentID.PosterPath = item.PosterPath;
                    contentID.ItemID = item.ItemID;

                    foreach (KeyValuePair<string, object?> ele in item.GetReplaceDict())
                    {
                        if (ele.Value is not null)
                        {
                            tmpEntry = this.TemplateReplace(tmpEntry, ele.Key, ele.Value);
                        }
                    }

                    builtHTMLString += tmpEntry.Replace("{TitleInfo}", seaEpsHtml, StringComparison.Ordinal)
                                                .Replace("{ImageURL}", "cid:<" + item.ItemID + ">", StringComparison.Ordinal);

                    contentIdList.Add(JsonConvert.SerializeObject(contentID));
                    completed.Add(item.Title);
                }
            }

            foreach (var row in db.Query("SELECT * FROM NewsletterData WHERE Emailed = 0 AND Type = 'Album';"))
            {
                if (row is not null)
                {
                    ContentIdJson contentID = new ContentIdJson();
                    JsonFileObj item = jsonHelper.ConvertToObj(row);
                    if (completed.Contains(item.Title))
                    {
                        continue;
                    }

                    string albumsHtml = string.Empty;
                    if (item.Type == "Album")
                    {
                        List<NlDetailsJson> parsedInfoList = ParseMusicInfo(item);
                        albumsHtml += GetSeasonEpisodeHTML(parsedInfoList);
                    }

                    var tmpEntry = config.Entry;
                    
                    contentID.PosterPath = item.PosterPath;
                    contentID.ItemID = item.ItemID;

                    foreach (KeyValuePair<string, object?> ele in item.GetReplaceDict())
                    {
                        if (ele.Value is not null)
                        {
                            tmpEntry = this.TemplateReplace(tmpEntry, ele.Key, ele.Value);
                        }
                    }

                    builtHTMLString += tmpEntry.Replace("{TitleInfo}", albumsHtml, StringComparison.Ordinal)
                                                .Replace("{ImageURL}", "cid:<" + item.ItemID + ">", StringComparison.Ordinal);
                    
                    contentIdList.Add(JsonConvert.SerializeObject(contentID));
                    completed.Add(item.Title);
                }
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

        return builtHTMLString;
    }

    public List<string> BuildContentId()
    {
        return contentIdList;
    }

    private string GetSeasonEpisodeHTML(List<NlDetailsJson> list)
    {
        string html = string.Empty;
        foreach (NlDetailsJson obj in list)
        {
            logger.Debug("SNIPPET OBJ: " + JsonConvert.SerializeObject(obj));
            // html += "<div id='SeasonEpisode' class='text' style='color: #FFFFFF;'>Season: " + obj.Season + " - Eps. " + obj.EpisodeRange + "</div>";
            if (obj.Type is "Series")
            {
                html += "Season: " + obj.Season + " - Eps. " + obj.EpisodeRange + "<br>";
            }
            else if (obj.Type is "Album")
            {
                html += "Album: " + obj.Album + "<br>";
            }
        }

        return html;
    }

    private List<NlDetailsJson> ParseSeriesInfo(JsonFileObj currObj)
    {
        List<NlDetailsJson> compiledList = new List<NlDetailsJson>();
        List<NlDetailsJson> finalList = new List<NlDetailsJson>();

        // Creates list of episodes + seasons for a series to be added to the newsletter.  Or individual movies.
        foreach (var row in db.Query("SELECT * FROM NewsletterData WHERE Emailed = 0 AND Title='" + currObj.Title + "';"))
        {
            if (row is not null)
            {
                JsonFileObj helper = new JsonFileObj();
                JsonFileObj itemObj = helper.ConvertToObj(row);

                NlDetailsJson tempVar = new NlDetailsJson()
                {
                    Title = itemObj.Title,
                    Season = itemObj.Season,
                    Episode = itemObj.Episode,
                    Type = itemObj.Type
                };

                logger.Debug("tempVar.Season: " + tempVar.Season + " : tempVar.Episode: " + tempVar.Episode);
                compiledList.Add(tempVar);
            }
        }

        List<int> tempEpsList = new List<int>();
        NlDetailsJson currSeriesDetailsObj = new NlDetailsJson();

        // Parse episode/season/movie list
        int currSeason = -1;
        bool newSeason = true;
        int list_len = compiledList.Count;
        int count = 1;
        foreach (NlDetailsJson item in SortListBySeason(SortListByEpisode(compiledList)))
        {
            logger.Debug("After Sort in foreach: Season::" + item.Season + "; Episode::" + item.Episode);
            logger.Debug("Count/list_len: " + count + "/" + list_len);
            currSeriesDetailsObj.Title = item.Title;

            // Inserts attributes from previous loops into final JSON object
            NlDetailsJson CopyJsonFromExisting(NlDetailsJson obj)
            {
                NlDetailsJson newJson = new NlDetailsJson();
                newJson.Season = obj.Season;
                newJson.EpisodeRange = obj.EpisodeRange;
                newJson.Type = obj.Type;
                return newJson;
            }

            void AddNewSeason()
            {
                logger.Debug("AddNewSeason()");
                currSeriesDetailsObj.Season = currSeason = item.Season;
                currSeriesDetailsObj.Type = item.Type;
                newSeason = false;
                tempEpsList.Add(item.Episode);
            }

            void AddCurrentSeason()
            {
                // logger.Debug("AddCurrentSeason()");
                logger.Debug("Seasons Match " + currSeason + "::" + item.Season);
                tempEpsList.Add(item.Episode);
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
                        string epList = string.Empty;
                        int firstRangeEp, prevEp;
                        firstRangeEp = prevEp = -1;

                        bool IsNext(int prev, int curr)
                        {
                            logger.Debug("Checking Prev and Curr..");
                            logger.Debug($"prev: {prev} :: curr: {curr}");
                            logger.Debug(prev + 1);
                            if (curr == prev + 1)
                            {
                                return true;
                            }

                            return false;
                        }

                        string ProcessEpString(int firstRangeEp, int prevEp)
                        {
                            if (firstRangeEp == prevEp)
                            {
                                epList += firstRangeEp + ",";
                            }
                            else
                            {
                                epList += firstRangeEp + "-" + prevEp + ",";
                            }

                            return epList;
                        }

                        foreach (int ep in tempEpsList)
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
                                continue;
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
                                continue;
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

            logger.Debug("CurrItem Season/Episode number: " + item.Season + "/" + item.Episode);
            if (newSeason)
            {
                AddNewSeason();
            }
            else if (currSeason == item.Season) // && (count < list_len))
            {
                AddCurrentSeason();
            }
            else if (count < list_len)
            {
                EndOfSeason();
                AddNewSeason();
            }
            else if (count == list_len)
            {
                EndOfSeason();
            }
            else
            {
                EndOfSeason();
            }

            if (count == list_len)
            {
                EndOfSeason();
            }

            count++;
        }

        logger.Debug("FinalList Length: " + finalList.Count);

        // Prevents entering foreach loop when debug logging is not enabled
        if (config.DebugMode)
        {
            foreach (NlDetailsJson item in finalList)
            {
                logger.Debug("FinalListObjs: " + JsonConvert.SerializeObject(item));
            }
        }

        return finalList;
    }

    private List<NlDetailsJson> ParseMusicInfo(JsonFileObj currObj)
    {
        List<NlDetailsJson> compiledList = new List<NlDetailsJson>();
        List<NlDetailsJson> finalList = new List<NlDetailsJson>();

        // Creates list of albums to be added to the newsletter
        foreach (var row in db.Query("SELECT * FROM NewsletterData WHERE Emailed = 0 AND Title='" + currObj.Title + "';"))
        {
            if (row is not null)
            {
                JsonFileObj helper = new JsonFileObj();
                JsonFileObj itemObj = helper.ConvertToObj(row);

                NlDetailsJson tempVar = new NlDetailsJson()
                {
                    Title = itemObj.Title,
                    Album = itemObj.Album,
                    Type = itemObj.Type
                };

                logger.Debug("tempVar.Album: " + tempVar.Album);
                compiledList.Add(tempVar);
            }
        }

        NlDetailsJson currAlbumDetailsObj = new NlDetailsJson();

        // Parses album list
        int list_len = compiledList.Count;
        int count = 1;
        foreach (NlDetailsJson item in compiledList)
        {
            logger.Debug("After Sort in foreach: Album::" + item.Album);
            logger.Debug("Count/list_len: " + count + "/" + list_len);
            currAlbumDetailsObj.Title = item.Title;

            // Inserts album attributes from previous loops into JSON object
            NlDetailsJson CopyJsonFromExisting(NlDetailsJson obj)
            {
                NlDetailsJson newJson = new NlDetailsJson();
                newJson.Album = obj.Album;
                newJson.Type = obj.Type;
                return newJson;
            }

            // Inserts album attributes from current loop into JSON object
            void AddNewAlbum()
            {
                currAlbumDetailsObj.Album = item.Album;
                currAlbumDetailsObj.Type = item.Type;
                finalList.Add(CopyJsonFromExisting(currAlbumDetailsObj));
            }

            logger.Debug("CurrItem Album: " + item.Album);
            if (count < list_len)
            {
                AddNewAlbum();
            }

            count++;
        }

        logger.Debug("FinalList Length: " + finalList.Count);

        // Prevents entering foreach loop when debug logging is not enabled
        if (config.DebugMode)
        {
            foreach (NlDetailsJson item in finalList)
            {
                logger.Debug("FinalListObjs: " + JsonConvert.SerializeObject(currAlbumDetailsObj));
            }
        }

        return finalList;
    }

    private bool IsIncremental(List<int> values)
    {
        return values.Skip(1).Select((v, i) => v == (values[i] + 1)).All(v => v);
    }

    private List<NlDetailsJson> SortListBySeason(List<NlDetailsJson> list)
    {
        return list.OrderBy(x => x.Season).ToList();
    }

    private List<NlDetailsJson> SortListByEpisode(List<NlDetailsJson> list)
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
        db.ExecuteSql("UPDATE NewsletterData SET Emailed = 1 WHERE Emailed = 0;");
        db.CloseConnection();
    }
}