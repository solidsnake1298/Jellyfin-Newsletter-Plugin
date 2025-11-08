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
using Jellyfin.Plugin.Newsletters.LOGGER;
using Jellyfin.Plugin.Newsletters.Scanner.NLImageHandler;
using Jellyfin.Plugin.Newsletters.Scripts.ENTITIES;
using Jellyfin.Plugin.Newsletters.Shared.DATA;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
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

    // Non-readonly
    private int totalLibCount;
    private int currCount;
    private PosterImageHandler imageHandler;
    private SQLiteDatabase db;
    private JsonFileObj jsonHelper;
    private Logger logger;
    private IProgress<double> progress;
    private CancellationToken cancelToken;

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
    public Task GetNewsletterData()
    {
        logger.Info("Gathering Data...");
        try
        {
            db.CreateConnection();
            BuildJsonObjsToCurrScanfile();
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
            InternalItemsQuery album = new InternalItemsQuery();
            album.IncludeItemTypes = new[] { BaseItemKind.MusicAlbum };
            BuildObjs(libManager.GetItemList(album).ToList(), "Album"); // populate music albums

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
        BaseItem episode, season, series, album, artist, movie;
        totalLibCount = items.Count;
        logger.Info($"Scan Size: {totalLibCount}");
        logger.Info($"Scanning '{type}'");
        foreach (BaseItem item in items)
        {
            logger.Debug("---------------");
            currCount++;
            progress.Report((double)currCount / (double)totalLibCount * 100);
            bool inDatabase = false;
            if (type == "Album")
            {
                string path = Path.GetDirectoryName(item.Path).Replace("'", string.Empty, StringComparison.Ordinal);
                inDatabase = InDatabase(path);
            }
            else
            {
                inDatabase = InDatabase(item.Path.Replace("'", string.Empty, StringComparison.Ordinal));
            }

            if ((item is not null) && !inDatabase)
            {
                JsonFileObj currFileObj = new JsonFileObj();
                try
                {
                    logger.Debug($"LocationType: " + item.LocationType.ToString());
                    logger.Debug($"LocationType: " + item.Path.ToString());
                    
                    if (item.LocationType.ToString() == "Virtual")
                    {
                        logger.Debug($"No physical path.. Skipping...");
                        continue;
                    }

                    if (type == "Series")
                    {
                        logger.Debug($"Found Series");
                        episode = item;
                        season = episode.FindParent<TVEntity.Season>();
                        series = episode.FindParent<TVEntity.Series>();
                        currFileObj.Type = type;
                        currFileObj = SeriesObj(episode, season, series, currFileObj);
                    }
                    else if (type == "Movie")
                    {
                        logger.Debug($"Found Movie");
                        movie = item;
                        currFileObj.Type = type;
                        currFileObj = MovieObj(movie, currFileObj);
                    }
                    else if (type == "Album")
                    {
                        logger.Debug($"Found Album");
                        album = item;
                        artist = album.FindParent<MusicArtist>();
                        currFileObj.Type = type;
                        currFileObj = MusicObj(album, artist, currFileObj);
                    }
                    else
                    {
                        logger.Error("Something went wrong..");
                        continue;
                    }

                    logger.Debug($"Checking if PosterPath Exists");
                    if ((currFileObj.PosterPath != null) && (currFileObj.PosterPath.Length > 0))
                    {
                        string url = imageHandler.FetchImagePoster(currFileObj);
                        logger.Debug("URL: " + url);

                        if ((url == "429") || (url == "ERR"))
                        {
                            logger.Debug("URL is not attainable at this time. Stopping scan.. Will resume during next scan.");
                        }

                        currFileObj.ImageURL = url;
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Error processing item..");
                    logger.Error(e);
                    continue;
                }
                finally
                {
                    // save to "database" : Table currRunScanList
                    logger.Debug("Adding to NewsletterData DB...");
                    currFileObj = NoNull(currFileObj);
                    db.ExecuteSQL("INSERT INTO NewsletterData (Filename, Title, Album, Season, Episode, Overview, ImageURL, ItemID, PosterPath, Type, Emailed) " +
                            "VALUES (" +
                                SanitizeDbItem(currFileObj.Filename) +
                                "," + SanitizeDbItem(currFileObj!.Title) +
                                "," + SanitizeDbItem(currFileObj!.Album) +
                                "," + ((currFileObj?.Season is null) ? -1 : currFileObj.Season) +
                                "," + ((currFileObj?.Episode is null) ? -1 : currFileObj.Episode) +
                                "," + SanitizeDbItem(currFileObj!.Overview) +
                                "," + SanitizeDbItem(currFileObj!.ImageURL) +
                                "," + SanitizeDbItem(currFileObj.ItemID) +
                                "," + SanitizeDbItem(currFileObj!.PosterPath) +
                                "," + SanitizeDbItem(currFileObj.Type) +
                                "," + ((currFileObj?.Emailed is null) ? 0 : currFileObj.Emailed) +
                            ");");
                    logger.Debug("Complete!");
                }
            }
            else if (item is null)
            {
                logger.Debug("Item is null!");
                continue;
            }
            else if (inDatabase)
            {
                logger.Debug("\"" + item.Path + "\" has already been processed either by Previous or Current Newsletter!");
                continue;
            }
        }
    }

    private JsonFileObj SeriesObj(BaseItem episode, BaseItem season, BaseItem series, JsonFileObj currFileObj)
    {
        currFileObj.Filename = episode.Path;
        currFileObj.Title = series.Name;
        currFileObj.Episode = (episode.IndexNumber is null) ? 0 : (int)episode.IndexNumber;
        currFileObj.Season = (season.IndexNumber is null) ? 0 : (int)season.IndexNumber;
        currFileObj.Album = string.Empty;
        currFileObj.Overview = series.Overview;
        currFileObj.ItemID = series.Id.ToString("N");
        currFileObj.Emailed = 0;

        logger.Debug($"ItemId: " + currFileObj.ItemID); // Series ItemId
        logger.Debug($"{currFileObj.Type}: {currFileObj.Title}"); // Series Title

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

        logger.Debug($"Season: {season.IndexNumber}");
        logger.Debug($"Episode Name: {episode.Name}");
        logger.Debug($"Episode Number: {episode.IndexNumber}");
        logger.Debug($"Overview: {series.Overview}");
        logger.Debug($"ImageInfo: {series.PrimaryImagePath}");
        logger.Debug($"Filepath: " + episode.Path);

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

        logger.Debug($"Movie: {movie.Name}");
        logger.Debug($"Overview: {movie.Overview}");
        logger.Debug($"ImageInfo: {movie.PrimaryImagePath}");
        logger.Debug($"Filepath: " + movie.Path);

        return currFileObj;
    }

    private JsonFileObj MusicObj(BaseItem album, BaseItem artist, JsonFileObj currFileObj)
    {
        currFileObj.Filename = album.Path;
        currFileObj.Title = artist.Name.ToString();
        currFileObj.Episode = -1;
        currFileObj.Season = -1;
        currFileObj.Album = album.Name.ToString();
        currFileObj.Overview = string.Empty;
        currFileObj.ItemID = album.Id.ToString("N");
        currFileObj.PosterPath = artist.PrimaryImagePath;
        currFileObj.Emailed = 0;

        logger.Debug($"Artist: {artist.Name.ToString()}");
        logger.Debug($"ImageInfo: {artist.PrimaryImagePath}");
        logger.Debug($"Filepath: " + album.Path);

        return currFileObj;
    }

    private JsonFileObj NoNull(JsonFileObj currFileObj)
    {
        currFileObj.Filename ??= string.Empty;
        currFileObj.Title ??= string.Empty;
        currFileObj.Album ??= string.Empty;
        currFileObj.Overview ??= string.Empty;
        currFileObj.ImageURL ??= string.Empty;
        currFileObj.ItemID ??= string.Empty;
        currFileObj.PosterPath ??= string.Empty;
        currFileObj.Type ??= string.Empty;

        return currFileObj;
    }

    private bool InDatabase(string fileName)
    {
        foreach (var row in db.Query("SELECT COUNT(*) FROM NewsletterData WHERE Filename='" + fileName + "';"))
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