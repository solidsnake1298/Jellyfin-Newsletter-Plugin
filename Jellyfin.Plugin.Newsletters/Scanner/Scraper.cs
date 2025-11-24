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
using Jellyfin.Database.Implementations.Entities.Libraries;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.NLPLogger;
using Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using Jellyfin.Plugin.Newsletters.Shared.DATA;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TVEntity = MediaBrowser.Controller.Entities.TV;

// using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Newsletters.Scripts.SCRAPER;

public class Scraper
{
    // Global Vars
    // Readonly
    private readonly PluginConfiguration config;
    private readonly ILibraryManager libManager;
    private readonly IRecordingsManager recManager;

    // Non-readonly
    private int totalLibCount;
    private int currCount;
    private SqlLiteDatabase db;
    private JsonFileObj jsonHelper;
    private Logger logger;
    private IProgress<double> progress;
    private CancellationToken cancelToken;
    private string[] liveTvRootPaths = Array.Empty<string>();

    public Scraper(ILibraryManager libraryManager, IRecordingsManager recordingManager, IProgress<double> passedProgress, CancellationToken cancellationToken)
    {
        logger = new Logger();
        jsonHelper = new JsonFileObj();
        progress = passedProgress;
        cancelToken = cancellationToken;
        config = Plugin.Instance!.Configuration;
        libManager = libraryManager;
        recManager = recordingManager;

        totalLibCount = currCount = 0;

        db = new SqlLiteDatabase();

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
    public Task GetNewsletterData()
    {
        logger.Info("Gathering Data...");
        try
        {
            db.InitDatabase();
            db.CreateConnection();
            BuildJsonObjsToCurrScanfile();
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            UpdatePreviousRunTimestamp();
            db.CloseConnection();
        }

        return Task.CompletedTask;
    }

    private void BuildJsonObjsToCurrScanfile()
    {
        // Retrieves time stamp of last successful scan, sets MinDateLastSaved
        // This avoids unnecessarily processing the entire library for each run
        var minDate = DateTime.Now;
        var lastRun = string.Empty;
        foreach (var row in db.Query("SELECT LastRun from PreviousRun WHERE ID = 0;"))
        {
            lastRun = row[0].ToString();
            logger.Debug($"lastRun (local time):: {lastRun}");
            minDate = DateTime.Parse(lastRun, System.Globalization.CultureInfo.InvariantCulture).ToLocalTime();
            logger.Debug($"minDate (UTC):: {minDate}");
        }

        // Finds collection folders to then parse to build a string array to omit live TV recordings from BuildObjs parsing
        var recordingRootPaths = recManager.GetRecordingFolders();
        var recordingRootArray = recordingRootPaths.SelectMany(e => e.Locations).ToArray();
        List<string> recordingRootList = new List<string>();
        foreach (var recPath in recordingRootArray)
        {
            logger.Debug($"Recording Path:: {recPath}");
            recordingRootList.Add($"{recPath}");
        }

        liveTvRootPaths = recordingRootList.ToArray();

        if (!config.SeriesEnabled && !config.MoviesEnabled && !config.MusicEnabled)
        {
            logger.Info("No Libraries Enabled In Config!");
        }

        if (config.SeriesEnabled)
        {
            var series = new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                MinDateLastSaved = minDate
            };
            BuildObjs(libManager.GetItemList(series).ToList(), "Series"); // populate series
        }

        if (config.MoviesEnabled)
        {
            var movie = new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                MinDateLastSaved = minDate
            };
            BuildObjs(libManager.GetItemList(movie).ToList(), "Movie"); // populate movies
        }

        if (config.MusicEnabled)
        {
            var album = new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
                MinDateLastSaved = minDate
            };
            BuildObjs(libManager.GetItemList(album).ToList(), "Album"); // populate music albums
        }
    }

    public void BuildObjs(List<BaseItem> items, string type)
    {
        logger.Info($"Parsing {type}..");
        BaseItem season, series, artist;
        totalLibCount = items.Count;
        logger.Info($"Scan Size: {totalLibCount}");
        logger.Info($"Scanning '{type}'");
        foreach (BaseItem item in items)
        {
            logger.Debug("---------------");
            currCount++;
            progress.Report(currCount / (double)totalLibCount * 100);
            if (item.Path is not null)
            {
                var isLiveTV = false;

                // Checks if entry is a live TV recording and skips if it is
                foreach (string liveTvRoot in liveTvRootPaths)
                {
                    isLiveTV = item!.Path.Contains(liveTvRoot, StringComparison.InvariantCulture);
                    if (isLiveTV)
                    {
                        break;
                    }
                }

                if (isLiveTV)
                {
                    logger.Debug($"{item.Path} is a live TV recording.  Skipping.");
                    continue;
                }

                var path = string.Empty;
                if (type == "Album")
                {
                    path = Path.GetDirectoryName(item.Path);
                }
                else
                {
                    path = item.Path;
                }

                // Checks if the item was previously processed.  False condition should never happen due to previous checks. 
                if (InDatabase(SanitizeDbItem(path!)))
                {
                    logger.Debug($"{path} has already been processed either by Previous or Current Newsletter!");
                    continue;
                }

                var currFileObj = new JsonFileObj();
                try
                {
                    logger.Debug($"LocationType: " + item.LocationType.ToString());
                    logger.Debug($"LocationType: " + item.Path?.ToString());
                    
                    if (item.LocationType.ToString() == "Virtual")
                    {
                        logger.Debug($"No physical path.. Skipping...");
                        continue;
                    }

                    if (type == "Series")
                    {
                        logger.Debug($"Found Series");
                        season = item.FindParent<TVEntity.Season>();
                        series = item.FindParent<TVEntity.Series>();
                        if ((series is null) || (season is null))
                        {
                            logger.Debug($"Season or Series is null, skipping...");
                            continue;
                        }

                        currFileObj.Type = type;
                        currFileObj = SeriesObj(item, season, series, currFileObj);
                    }
                    else if (type == "Movie")
                    {
                        logger.Debug($"Found Movie");
                        currFileObj.Type = type;
                        currFileObj = MovieObj(item, currFileObj);
                    }
                    else if (type == "Album")
                    {
                        logger.Debug($"Found Album");
                        artist = item.FindParent<MusicArtist>();
                        currFileObj.Type = type;
                        currFileObj = MusicObj(item, artist, currFileObj);
                    }
                    else
                    {
                        logger.Error("Something went wrong..");
                        continue;
                    }

                    try
                    {
                        logger.Debug($"Checking if PosterPath Exists");
                        ArgumentNullException.ThrowIfNull(currFileObj.PosterPath);
                    }
                    catch
                    {
                        logger.Debug($"PosterPath is empty");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"Error processing item::{item.Path}");
                    logger.Error(e);
                    continue;
                }
                finally
                {
                    // save to "database" : Table NewsletterData
                    logger.Debug("Adding to NewsletterData DB...");
                    try
                    {
                        db.ExecuteSql(
                            "INSERT INTO NewsletterData (Filename, Title, Album, Season, Episode, Overview, ItemID, PosterPath, Type, Emailed) " +
                            "VALUES (" +
                            SanitizeDbItem(currFileObj.Filename) +
                            "," + SanitizeDbItem(currFileObj!.Title) +
                            "," + SanitizeDbItem(currFileObj!.Album) +
                            "," + currFileObj.Season +
                            "," + currFileObj.Episode +
                            "," + SanitizeDbItem(currFileObj!.Overview) +
                            "," + SanitizeDbItem(currFileObj.ItemID) +
                            "," + SanitizeDbItem(currFileObj!.PosterPath) +
                            "," + SanitizeDbItem(currFileObj.Type) +
                            "," + currFileObj?.Emailed +
                            ");");
                        logger.Debug("Complete!");
                    }
                    catch
                    {
                        logger.Debug("Failed to insert record.  Duplicate?");
                    }
                }
            }
            else
            {
                logger.Debug("Item Path is null!");
                continue;
            }
        }
    }

    private JsonFileObj SeriesObj(BaseItem episode, BaseItem season, BaseItem series, JsonFileObj currFileObj)
    {
        currFileObj.Filename = episode.Path;
        currFileObj.Title = series.Name;
        currFileObj.Episode = episode.IndexNumber ??= 0;
        currFileObj.Season = season.IndexNumber ??= 0;
        currFileObj.Album = string.Empty;
        currFileObj.Overview = series.Overview;
        currFileObj.ItemID = series.Id.ToString("N");
        currFileObj.Emailed = 0;

        logger.Debug($"ItemID: " + currFileObj.ItemID); // Series ItemID
        logger.Debug($"{currFileObj.Type}: {currFileObj.Title}"); // Series Title

        if (series.PrimaryImagePath is not null)
        {
            logger.Debug("Primary Image series found!");
            currFileObj.PosterPath = series.PrimaryImagePath;
        }
        else if (episode.PrimaryImagePath is not null)
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

        logger.Debug($"Season: {currFileObj.Season}");
        logger.Debug($"Episode Number: {currFileObj.Episode}");
        logger.Debug($"Overview: {currFileObj.Overview}");
        logger.Debug($"ImageInfo: {currFileObj.PosterPath}");
        logger.Debug($"Filepath: {currFileObj.Filename}");
        logger.Debug($"DateLastSaved:: {episode.DateLastSaved}");

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
        currFileObj.Emailed = 0;

        if (movie.PrimaryImagePath is not null)
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

        logger.Debug($"Movie: {currFileObj.Title}");
        logger.Debug($"Overview: {currFileObj.Overview}");
        logger.Debug($"ImageInfo: {currFileObj.PosterPath}");
        logger.Debug($"Filepath: {currFileObj.Filename}");

        return currFileObj;
    }

    private JsonFileObj MusicObj(BaseItem album, BaseItem artist, JsonFileObj currFileObj)
    {
        currFileObj.Filename = album.Path;
        currFileObj.Title = artist.Name;
        currFileObj.Episode = -1;
        currFileObj.Season = -1;
        currFileObj.Album = album.Name;
        currFileObj.Overview = string.Empty;
        currFileObj.ItemID = album.Id.ToString("N");
        currFileObj.PosterPath = artist.PrimaryImagePath;
        currFileObj.Emailed = 0;

        currFileObj.PosterPath ??= album.PrimaryImagePath;

        logger.Debug($"Artist: {currFileObj.Title}");
        logger.Debug($"ImageInfo: {currFileObj.PosterPath}");
        logger.Debug($"Filepath: {currFileObj.Filename}");

        return currFileObj;
    }

    private bool InDatabase(string fileName)
    {
        foreach (var row in db.Query("SELECT COUNT(*) FROM NewsletterData WHERE Filename=" + fileName + ";"))
        {
            if (row is not null)
            {
                if (int.TryParse(row[0].ToString(), out var x) && x > 0)
                {
                    logger.Debug("NewsletterData Size: " + row[0].ToString());
                    return true;
                }
            }
        }

        return false;
    }

    private string SanitizeDbItem(string unsanitizedString)
    {
        // string sanitize_string = string.Empty;
        if (unsanitizedString is null)
        {
            unsanitizedString = string.Empty;
        }

        return "'" + unsanitizedString.Replace("'", string.Empty, StringComparison.Ordinal) + "'";
    }

    private void UpdatePreviousRunTimestamp()
    {
        DateTime currDate = DateTime.UtcNow;
        db.ExecuteSql("UPDATE PreviousRun SET LastRun = '" + currDate + "' WHERE ID = 0;");
    }
}