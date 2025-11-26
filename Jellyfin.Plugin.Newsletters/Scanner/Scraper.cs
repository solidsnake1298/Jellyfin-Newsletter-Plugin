#pragma warning disable 1591, CA1002, SA1005 // remove SA1005 to clean code
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.Shared.Database;
using Jellyfin.Plugin.Newsletters.Shared.Entities;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using TVEntity = MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.Newsletters.Scanner;

public class Scraper
{
    // Global Vars
    // Readonly
    private readonly PluginConfiguration config;
    private readonly ILibraryManager libManager;
    private readonly IRecordingsManager recManager;
    private readonly IDtoService dtoService;
    private readonly SqLiteDatabase db;
    private readonly Logger logger;
    private readonly IProgress<double> progress;

    // Non-readonly
    private int totalLibCount;
    private int currCount;
    private string[] liveTvRootPaths = 
        [];

    public Scraper(ILibraryManager libraryManager, IRecordingsManager recordingManager, IDtoService dtoServiceProvider, IProgress<double> passedProgress)
    {
        logger = new Logger();
        progress = passedProgress;
        config = Plugin.Instance!.Configuration;
        libManager = libraryManager;
        recManager = recordingManager;
        dtoService = dtoServiceProvider;

        totalLibCount = currCount = 0;

        db = new SqLiteDatabase();

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
            BuildJsonObjsToCurrScanFile();
        }
        catch (Exception e)
        {
            logger.Error("An error has occured: " + e);
        }
        finally
        {
            UpdatePreviousRunTimestamp();
            db.CloseConnection();
            progress.Report(100);
            logger.Info("Completed scanning for new items.");
        }

        return Task.CompletedTask;
    }

    private void BuildJsonObjsToCurrScanFile()
    {
        // Retrieves time stamp of last successful scan, sets MinDateLastSaved
        // This avoids unnecessarily processing the entire library for each run
        var minDate = DateTime.Now;
        foreach (var row in db.Query("SELECT LastRun from PreviousRun WHERE ID = 0;"))
        {
            var lastRun = row[0].ToString();
            logger.Debug($"lastRun (local time):: {lastRun}");
            minDate = DateTime.Parse(lastRun, System.Globalization.CultureInfo.InvariantCulture).ToLocalTime();
            logger.Debug($"minDate (UTC):: {minDate}");
        }

        // Finds collection folders to then parse to build a string array to omit live TV recordings from BuildObjs parsing
        var recordingRootPaths = recManager.GetRecordingFolders();
        var recordingRootArray = recordingRootPaths.SelectMany(e => e.Locations).ToArray();
        var recordingRootList = new List<string>();
        foreach (var recPath in recordingRootArray)
        {
            logger.Debug($"Recording Path:: {recPath}");
            recordingRootList.Add($"{recPath}");
        }

        liveTvRootPaths = recordingRootList.ToArray();

        if (config is { SeriesEnabled: false, MoviesEnabled: false, MusicEnabled: false })
        {
            logger.Info("No Libraries Enabled In Config!");
        }

        if (config.SeriesEnabled)
        {
            var series = new InternalItemsQuery()
            {
                IncludeItemTypes = 
                    [BaseItemKind.Episode],
                MinDateLastSaved = minDate
            };
            BuildObjs(libManager.GetItemList(series).ToList(), "Series"); // populate series
        }

        if (config.MoviesEnabled)
        {
            var movie = new InternalItemsQuery()
            {
                IncludeItemTypes = 
                    [BaseItemKind.Movie],
                MinDateLastSaved = minDate
            };
            BuildObjs(libManager.GetItemList(movie).ToList(), "Movie"); // populate movies
        }

        if (config.MusicEnabled)
        {
            var album = new InternalItemsQuery()
            {
                IncludeItemTypes = 
                    [BaseItemKind.MusicAlbum],
                MinDateLastSaved = minDate
            };
            BuildObjs(libManager.GetItemList(album).ToList(), "Album"); // populate music albums
        }
    }

    private void BuildObjs(List<BaseItem> items, string type)
    {
        logger.Info($"Parsing {type}..");
        totalLibCount = items.Count;
        logger.Info($"Scan Size: {totalLibCount}");
        logger.Info($"Scanning '{type}'");
        foreach (var item in items)
        {
            logger.Debug("---------------");
            currCount++;
            progress.Report(currCount / (double)totalLibCount * 100);
            if (item.Path is not null)
            {
                var isLiveTv = false;

                // Checks if entry is a live TV recording and skips if it is
                foreach (var liveTvRoot in liveTvRootPaths)
                {
                    isLiveTv = item.Path.Contains(liveTvRoot, StringComparison.InvariantCulture);
                    if (isLiveTv)
                    {
                        break;
                    }
                }

                if (isLiveTv)
                {
                    logger.Debug($"{item.Path} is a live TV recording.  Skipping.");
                    continue;
                }

                var path = type == "Album" ? Path.GetDirectoryName(item.Path) : item.Path;

                // Checks if the item was previously processed.  False condition should never happen due to previous checks. 
                if (InDatabase(SanitizeDbItem(path!)))
                {
                    logger.Debug($"{path} has already been processed either by Previous or Current Newsletter!");
                    continue;
                }

                var currFileObj = new JsonFileObj();
                try
                {
                    if (item.LocationType.ToString() == "Virtual")
                    {
                        logger.Debug($"No physical path.. Skipping...");
                        continue;
                    }

                    switch (type)
                    {
                        case "Series":
                        {
                            logger.Debug($"Found Series");
                            BaseItem? season = item.FindParent<TVEntity.Season>();
                            BaseItem series = item.FindParent<TVEntity.Series>();
                            if (season is null)
                            {
                                logger.Debug("Season is null, using DTO service to retrieve season BaseItem...");
                                var dtoOptions = new DtoOptions(false) { EnableImages = false };
                                var dto = dtoService.GetBaseItemDto(item, dtoOptions);
                                var emptyGuid = new Guid("00000000-0000-0000-0000-000000000000");
                                var seasonId = dto.SeasonId ??= emptyGuid;
                                season = libManager.GetItemById(seasonId);
                                logger.Debug($"SeasonID:: {seasonId}");
                                if (season is null)
                                {
                                    logger.Debug("DTO service couldn't retrieve a season BaseItem.  Skipping...");
                                    continue;
                                }
                                
                                logger.Debug("DTO service successfully retrieve season BaseItem...");
                            }

                            currFileObj.Type = type;
                            currFileObj = SeriesObj(item, season, series, currFileObj);
                            break;
                        }
                        
                        case "Movie":
                            logger.Debug($"Found Movie");
                            currFileObj.Type = type;
                            currFileObj = MovieObj(item, currFileObj);
                            break;
                        case "Album":
                        {
                            logger.Debug($"Found Album");
                            BaseItem artist = item.FindParent<MusicArtist>();
                            currFileObj.Type = type;
                            currFileObj = MusicObj(item, artist, currFileObj);
                            break;
                        }
                        
                        default:
                            logger.Error("Something went wrong..");
                            continue;
                    }

                    try
                    {
                        logger.Debug("Checking if PosterPath Exists");
                        ArgumentNullException.ThrowIfNull(currFileObj.PosterPath);
                    }
                    catch
                    {
                        logger.Debug($"PosterPath is empty");
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"Error processing item::{item.Path}");
                    logger.Error(e);
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
                            "," + SanitizeDbItem(currFileObj.Title) +
                            "," + SanitizeDbItem(currFileObj.Album) +
                            "," + currFileObj.Season +
                            "," + currFileObj.Episode +
                            "," + SanitizeDbItem(currFileObj.Overview) +
                            "," + SanitizeDbItem(currFileObj.ItemId) +
                            "," + SanitizeDbItem(currFileObj.PosterPath) +
                            "," + SanitizeDbItem(currFileObj.Type) +
                            "," + currFileObj.Emailed +
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
                logger.Error("Item Path is null!");
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
        currFileObj.ItemId = series.Id.ToString("N");
        currFileObj.Emailed = 0;

        logger.Debug($"ItemID: " + currFileObj.ItemId); // Series ItemID
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
        currFileObj.ItemId = movie.Id.ToString("N");
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
        currFileObj.ItemId = album.Id.ToString("N");
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
            if (int.TryParse(row[0].ToString(), out var x) && x > 0)
            {
                logger.Debug("NewsletterData Size: " + row[0]);
                return true;
            }
        }

        return false;
    }

    private static string SanitizeDbItem(string unsanitizedString)
    {
        return "'" + unsanitizedString.Replace("'", string.Empty, StringComparison.Ordinal) + "'";
    }

    private void UpdatePreviousRunTimestamp()
    {
        var currDate = DateTime.UtcNow;
        db.ExecuteSql("UPDATE PreviousRun SET LastRun = '" + currDate + "' WHERE ID = 0;");
    }
}