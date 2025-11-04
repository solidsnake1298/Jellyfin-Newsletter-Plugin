#pragma warning disable 1591, CA1002, SA1005 // remove SA1005 to clean code
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.LOGGER;
using Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using Jellyfin.Plugin.Newsletters.Shared.DATA;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Newsletters.Scripts.SCRAPER;

public class Scraper
{
    // Global Vars
    // Readonly
    private readonly PluginConfiguration config;
    // private readonly string currRunScanList;
    // private readonly string archiveFile;
    // private readonly string currNewsletterDataFile;
    private readonly ILibraryManager libManager;

    // Non-readonly
    private int totalLibCount;
    private int currCount;
    private PosterImageHandler imageHandler;
    private SQLiteDatabase db;
    private JsonFileObj jsonHelper;
    private Logger logger;
    private IProgress<double> progress;
    private CancellationToken cancelToken;
    // private List<JsonFileObj> archiveObj;

    public Scraper(ILibraryManager libraryManager, IProgress<double> passedProgress, CancellationToken cancellationToken)
    {
        logger = new Logger();
        jsonHelper = new JsonFileObj();
        progress = passedProgress;
        cancelToken = cancellationToken;
        config = Plugin.Instance!.Configuration;
        libManager = libraryManager;

        totalLibCount = currCount = 0;

        imageHandler = new PosterImageHandler();
        db = new SQLiteDatabase();

        logger.Debug("Setting Config Paths: ");
        logger.Debug("\n  DataPath: " + config.DataPath +
                     "\n  TempDirectory: " + config.TempDirectory +
                     "\n  PluginsPath: " + config.PluginsPath +
                     "\n  NewsletterDir: " + config.NewsletterDir +
                     "\n  ProgramDataPath: " + config.ProgramDataPath +
                     "\n  ProgramSystemPath: " + config.ProgramSystemPath +
                     "\n  SystemConfigurationFilePath: " + config.SystemConfigurationFilePath +
                     "\n  LogDirectoryPath: " + config.LogDirectoryPath );
    }

    // This is the main function
    public Task GetSeriesData()
    {
        logger.Info("Gathering Data...");
        try
        {
            db.CreateConnection();
            BuildJsonObjsToCurrScanfile();
            CopyCurrRunDataToNewsletterData();
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            db.CloseConnection();
        }

        return Task.CompletedTask;
    }

    private void BuildJsonObjsToCurrScanfile()
    {
        if (!config.SeriesEnabled && !config.MoviesEnabled && !config.MusicEnabled)
        {
            logger.Info("No Libraries Enabled In Config!");
        }

        if (config.SeriesEnabled)
        {
            InternalItemsQuery series = new InternalItemsQuery();
            series.IncludeItemTypes = new[] { BaseItemKind.Episode };
            BuildObjs(libManager.GetItemList(series).ToList(), "Series"); // populate series

            // BuildObjs(
            //     libManager.GetItemList(new InternalItemsQuery
            //     {
            //         IncludeItemTypes = new[] { BaseItemKind.Episode }
            //     }),
            //     "Series"); 
        }

        if (config.MoviesEnabled)
        {
            InternalItemsQuery movie = new InternalItemsQuery();
            movie.IncludeItemTypes = new[] { BaseItemKind.Movie };
            BuildObjs(libManager.GetItemList(movie).ToList(), "Movie"); // populate movies

            // BuildObjs(
            //     libManager.GetItemList(new InternalItemsQuery
            //     {
            //         IncludeItemTypes = new[] { BaseItemKind.Movie }
            //     }),
            //     "Movie");
        }

        if (config.MusicEnabled)
        {
            InternalItemsQuery audio = new InternalItemsQuery();
            audio.IncludeItemTypes = new[] { BaseItemKind.Audio };
            BuildObjs(libManager.GetItemList(audio).ToList(), "Music"); // populate music

            // BuildObjs(
            //     libManager.GetItemList(new InternalItemsQuery
            //     {
            //         IncludeItemTypes = new[] { BaseItemKind.Audio }
            //     }),
            //     "Music");
        }
    }

    public void BuildObjs(List<BaseItem> items, string type)
    {
        logger.Info($"Parsing {type}..");
        BaseItem episode, season, series, song, album, artist, movie;
        totalLibCount = items.Count;
        logger.Info($"Scan Size: {totalLibCount}");
        logger.Info($"Scanning '{type}'");
        foreach (BaseItem item in items)
        {
            logger.Debug("---------------");
            currCount++;
            progress.Report((double)currCount / (double)totalLibCount * 100);
            if (item is not null)
            {
                JsonFileObj currFileObj = new JsonFileObj();
                try
                {
                    logger.Debug($"LocationType: " + item.LocationType.ToString());
                    if (item.LocationType.ToString() == "Virtual")
                    {
                        logger.Debug($"No physical path.. Skipping...");
                        continue;
                    }

                    if (type == "Series")
                    {
                        episode = item;
                        season = item.GetParent();
                        series = item.GetParent().GetParent();
                        currFileObj.Type = type;
                        currFileObj = SeriesObj(episode, series, season, currFileObj);
                    }
                    else if (type == "Movie")
                    {
                        movie = item;
                        currFileObj.Type = type;
                        currFileObj = MovieObj(movie, currFileObj);
                    }
                    else if (type == "Music")
                    {
                        song = item;
                        album = item.GetParent();
                        if (album.IsFolder is true)
                        {
                            album = song.GetParent().GetParent();
                        }

                        artist = album.GetParent();
                        currFileObj.Type = type;
                        currFileObj = MusicObj(song, album, artist, currFileObj);
                    }
                    else
                    {
                        logger.Error("Something went wrong..");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Error processing item..");
                    logger.Error(e);
                    continue;
                }

                if (!InDatabase("CurrRunData", currFileObj.Filename.Replace("'", string.Empty, StringComparison.Ordinal)) && 
                    !InDatabase("CurrNewsletterData", currFileObj.Filename.Replace("'", string.Empty, StringComparison.Ordinal)) && 
                    !InDatabase("ArchiveData", currFileObj.Filename.Replace("'", string.Empty, StringComparison.Ordinal)))
                {
                    try
                    {
                        logger.Debug("Checking if PosterPath Exists");
                        if ((currFileObj.PosterPath != null) && (currFileObj.PosterPath.Length > 0))
                        {
                            string url = SetImageURL(currFileObj);

                            if ((url == "429") || (url == "ERR"))
                            {
                                logger.Debug("URL is not attainable at this time. Stopping scan.. Will resume during next scan.");
                                logger.Debug("Not processing current file: " + currFileObj.Filename);
                                break;
                            }

                            currFileObj.ImageURL = url;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error($"Encountered an error parsing: {currFileObj.Filename}");
                        logger.Error(e);
                    }
                    finally
                    {
                        // save to "database" : Table currRunScanList
                        logger.Debug("Adding to CurrRunData DB...");
                        currFileObj = NoNull(currFileObj);
                        db.ExecuteSQL("INSERT INTO CurrRunData (Filename, Title, Album, Season, Episode, Overview, ImageURL, ItemID, PosterPath, Type) " +
                                "VALUES (" +
                                    SanitizeDbItem(currFileObj.Filename) +
                                    "," + SanitizeDbItem(currFileObj!.Title) +
                                    "," + ((currFileObj?.Season is null) ? -1 : currFileObj.Season) +
                                    "," + ((currFileObj?.Episode is null) ? -1 : currFileObj.Episode) +
                                    "," + ((currFileObj?.Album is null) ? -1 : currFileObj.Album) +
                                    "," + SanitizeDbItem(currFileObj!.Overview) +
                                    "," + SanitizeDbItem(currFileObj!.ImageURL) +
                                    "," + SanitizeDbItem(currFileObj.ItemID) +
                                    "," + SanitizeDbItem(currFileObj!.PosterPath) +
                                    "," + SanitizeDbItem(currFileObj.Type) +
                                ");");
                        logger.Debug("Complete!");
                    }
                }
                else
                {
                    logger.Debug("\"" + currFileObj.Filename + "\" has already been processed either by Previous or Current Newsletter!");
                }
            }
        }
    }

    private JsonFileObj SeriesObj(BaseItem episode, BaseItem season, BaseItem series, JsonFileObj currFileObj)
    {
        currFileObj.Filename = episode.Path;
        currFileObj.Title = series.GetParent().GetParent().Name;
        currFileObj.Episode = episode.IndexNumber ?? 0;
        currFileObj.Season = season.IndexNumber ?? 0;
        currFileObj.Album = string.Empty;
        currFileObj.Overview = series.Overview;
        currFileObj.ItemID = episode.Id.ToString("N");

        logger.Debug($"ItemId: " + currFileObj.ItemID); // series ItemId
        logger.Debug($"{currFileObj.Type}: {currFileObj.Title}"); // Title

        if (series.PrimaryImagePath != null)
        {
            logger.Debug("Primary Image series found!");
            currFileObj.PosterPath = series.PrimaryImagePath;
        }
        else if (episode.PrimaryImagePath != null)
        {
            logger.Debug("Primary Image series not found. Pulling from Episode");
            currFileObj.PosterPath = episode.PrimaryImagePath;
        }
        else
        {
            logger.Warn("Primary Poster not found..");
            logger.Warn("This may be due to filesystem not being formatted properly.");
            logger.Warn($"Make sure {currFileObj.Filename} follows the correct formatting below:");
            logger.Warn(".../MyLibraryName/Series_Name/Season#_or_Specials/Episode.{ext}");
        }

        logger.Debug($"Season: {season.Name}"); // Season Name
        logger.Debug($"Episode Name: {episode.Name}"); // episode Name
        logger.Debug($"Episode Number: {episode.IndexNumber}"); // episode Name
        logger.Debug($"Overview: {series.Overview}"); // series overview
        logger.Debug($"ImageInfo: {series.PrimaryImagePath}");
        logger.Debug($"Filepath: " + episode.Path); // Filepath, episode.Path is cleaner, but may be empty

        // NEW PARAMS
        logger.Debug($"PremiereDate: {series.PremiereDate}"); // series PremiereDate
        logger.Debug($"OfficialRating: " + series.OfficialRating); // TV-14, TV-PG, etc
        // logger.Info($"CriticRating: " + series.CriticRating);
        // logger.Info($"CustomRating: " + series.CustomRating);
        logger.Debug($"CommunityRating: " + series.CommunityRating); // 8.5, 9.2, etc

        return currFileObj;
    }

    private JsonFileObj MovieObj(BaseItem movie, JsonFileObj currFileObj)
    {
        currFileObj.Filename = movie.Path;
        currFileObj.Title = movie.Name;
        currFileObj.Episode = -1;
        currFileObj.Season = -1;
        currFileObj.Album = string.Empty;
        currFileObj.Overview = movie.Overview;
        currFileObj.ItemID = movie.Id.ToString("N");

        if (movie.PrimaryImagePath != null)
        {
            logger.Debug("Primary Image series found!");
            currFileObj.PosterPath = movie.PrimaryImagePath;
        }
        else
        {
            logger.Warn("Primary Poster not found..");
            logger.Warn("This may be due to filesystem not being formatted properly.");
            logger.Warn($"Make sure {currFileObj.Filename} follows the correct formatting below:");
            logger.Warn(".../MyLibraryName/Movie_Name/Movie.{ext}");
        }

        logger.Debug($"Movie: {movie.Name}"); // Season Name
        logger.Debug($"Overview: {movie.Overview}"); // series overview
        logger.Debug($"ImageInfo: {movie.PrimaryImagePath}");
        logger.Debug($"Filepath: " + movie.Path); // Filepath, episode.Path is cleaner, but may be empty

        return currFileObj;
    }

    private JsonFileObj MusicObj(BaseItem song, BaseItem album, BaseItem artist, JsonFileObj currFileObj)
    {
        currFileObj.Filename = album.Path;
        currFileObj.Title = artist.Name;
        currFileObj.Episode = -1;
        currFileObj.Season = -1;
        currFileObj.Album = album.Name;
        currFileObj.Overview = album.Overview;
        currFileObj.ItemID = album.Id.ToString("N");

        if (artist.PrimaryImagePath != null)
        {
            logger.Debug("Primary Image series found!");
            currFileObj.PosterPath = artist.PrimaryImagePath;
        }
        else
        {
            logger.Warn("Primary Poster not found..");
            logger.Warn("This may be due to filesystem not being formatted properly.");
            logger.Warn($"Make sure {currFileObj.Filename} follows the correct formatting below:");
            logger.Warn(".../MyLibraryName/Movie_Name/Movie.{ext}");
        }

        logger.Debug($"Artist: {artist.Name}"); // Artist name
        logger.Debug($"Overview: {album.Overview}"); // Album overview
        logger.Debug($"ImageInfo: {album.PrimaryImagePath}");
        logger.Debug($"Filepath: " + album.Path); // Filepath, episode.Path is cleaner, but may be empty

        return currFileObj;
    }

    private JsonFileObj NoNull(JsonFileObj currFileObj)
    {
        if (currFileObj.Filename == null)
        {
            currFileObj.Filename = string.Empty;
        }

        if (currFileObj.Title == null)
        {
            currFileObj.Title = string.Empty;
        }

        if (currFileObj.Album == null)
        {
            currFileObj.Album = string.Empty;
        }

        if (currFileObj.Overview == null)
        {
            currFileObj.Overview = string.Empty;
        }

        if (currFileObj.ImageURL == null)
        {
            currFileObj.ImageURL = string.Empty;
        }

        if (currFileObj.ItemID == null)
        {
            currFileObj.Filename = string.Empty;
        }

        if (currFileObj.PosterPath == null)
        {
            currFileObj.PosterPath = string.Empty;
        }

        return currFileObj;
    }

    private bool InDatabase(string tableName, string fileName)
    {
        foreach (var row in db.Query("SELECT COUNT(*) FROM " + tableName + " WHERE Filename='" + fileName + "';"))
        {
            if (row is not null)
            {
                if (int.Parse(row[0].ToString(), CultureInfo.CurrentCulture) > 0)
                {
                    logger.Debug(tableName + " Size: " + row[0].ToString());
                    return true;
                }
            }
        }

        return false;
    }

    private string SetImageURL(JsonFileObj currObj)
    {
        JsonFileObj fileObj;
        string currTitle = currObj.Title.Replace("'", string.Empty, StringComparison.Ordinal);

        // check if URL for series already exists CurrRunData table
        foreach (var row in db.Query("SELECT * FROM CurrRunData;"))
        {
            if (row is not null)
            {
                fileObj = jsonHelper.ConvertToObj(row);
                if ((fileObj is not null) && (fileObj.Title == currTitle) && (fileObj.ImageURL.Length > 0))
                {
                    logger.Debug("Found Current Scan of URL for " + currTitle + " :: " + fileObj.ImageURL);
                    return fileObj.ImageURL;
                }
            }
        }

        // check if URL for series already exists CurrNewsletterData table
        logger.Debug("Checking if exists in CurrNewsletterData");
        foreach (var row in db.Query("SELECT * FROM CurrNewsletterData;"))
        {
            if (row is not null)
            {
                fileObj = jsonHelper.ConvertToObj(row);
                if ((fileObj is not null) && (fileObj.Title == currTitle) && (fileObj.ImageURL.Length > 0))
                {
                    logger.Debug("Found Current Scan of URL for " + currTitle + " :: " + fileObj.ImageURL);
                    return fileObj.ImageURL;
                }
            }
        }

        // check if URL for series already exists ArchiveData table
        foreach (var row in db.Query("SELECT * FROM ArchiveData;"))
        {
            if (row is not null)
            {
                fileObj = jsonHelper.ConvertToObj(row);
                if ((fileObj is not null) && (fileObj.Title == currTitle) && (fileObj.ImageURL.Length > 0))
                {
                    logger.Debug("Found Current Scan of URL for " + currTitle + " :: " + fileObj.ImageURL);
                    return fileObj.ImageURL;
                }
            }
        }

        logger.Debug("Uploading poster...");
        logger.Debug(currObj.ItemID);
        logger.Debug(currObj.PosterPath);
        // return string.Empty;
        return imageHandler.FetchImagePoster(currObj);
    }

    private void CopyCurrRunDataToNewsletterData()
    {
        // -> copy CurrData Table to NewsletterDataTable
        // -> clear CurrData table
        db.ExecuteSQL("INSERT INTO CurrNewsletterData SELECT * FROM CurrRunData;");
        db.ExecuteSQL("DELETE FROM CurrRunData;");
    }

    private string SanitizeDbItem(string unsanitized_string)
    {
        // string sanitize_string = string.Empty;
        if (unsanitized_string is null)
        {
            unsanitized_string = string.Empty;
        }

        return "'" + unsanitized_string.Replace("'", string.Empty, StringComparison.Ordinal) + "'";
    }
}