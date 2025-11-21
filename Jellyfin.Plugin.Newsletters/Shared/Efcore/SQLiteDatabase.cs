//#pragma warning disable 1591, CA1304
//using System;
//using System.Collections.Generic;
//using System.IO;
//using Jellyfin.Plugin.Newsletters.Configuration;
//using Jellyfin.Plugin.Newsletters.NLPLogger;
//using Jellyfin.Plugin.Newsletters.Scripts.Entities;
//using MediaBrowser.Common.Configuration;
//using SQLitePCL;
//using SQLitePCL.pretty;
//
//namespace Jellyfin.Plugin.Newsletters.Shared.Data;
//
//public class SQLiteDatabase
//{
//    private readonly PluginConfiguration config;
//    private string dbFilePath;
//    private string dbLockPath;
//    private Logger logger;
//    private SQLiteDatabaseConnection? _db;
//
//    public SQLiteDatabase()
//    {
//        logger = new Logger();
//        config = Plugin.Instance!.Configuration;
//        SQLite3.EnableSharedCache = false;
//
//        _ = raw.sqlite3_config(raw.SQLITE_CONFIG_MEMSTATUS, 0);
//
//        _ = raw.sqlite3_config(raw.SQLITE_CONFIG_MULTITHREAD, 1);
//
//        _ = raw.sqlite3_enable_shared_cache(1);
//
//        ThreadSafeMode = raw.sqlite3_threadsafe();
//        dbFilePath = config.DataPath + "/newsletters.db"; // get directory from config
//        dbLockPath = dbFilePath + ".lock";
//    }
//
//    internal static int ThreadSafeMode { get; set; }
//
//    public void CreateConnection()
//    {
//        if (!File.Exists(dbLockPath)) // Database is not locked
//        {
//            logger.Debug("Opening Database: " + dbFilePath);
//            _db = SQLite3.Open(dbFilePath);
//            File.WriteAllText(dbLockPath, string.Empty);
//            InitDatabase();
//        }
//        else
//        {
//            logger.Debug("Database lock file shows database is in use: " + dbLockPath);
//        }
//    }
//
//    private void InitDatabase()
//    {
//       logger.Debug("Creating Tables...");
//       CreateTables();
//       MigrateTables();
//       logger.Debug("Done Init of tables");
//    }
//
//    private void CreateTables()
//    {
//        ExecuteSQL("CREATE TABLE IF NOT EXISTS NewsletterData (" +
//                "Filename TEXT NOT NULL," +
//                "Title TEXT," +
//                "Album TEXT," +
//                "Season INT," +
//                "Episode INT," +
//                "Overview TEXT," +
//                "ItemID TEXT," +
//                "PosterPath TEXT," +
//                "Type TEXT," +
//                "Emailed INT," +
//                "PRIMARY KEY (Filename));");
//        ExecuteSQL("CREATE TABLE IF NOT EXISTS PreviousRun (" +
//                "ID INTEGER NOT NULL," +
//                "LastRun TEXT," +
//                "PRIMARY KEY (ID));");
//        ExecuteSQL("CREATE TRIGGER IF NOT EXISTS PreviousRunNoInsert " + 
//                "BEFORE INSERT ON PreviousRun " +
//                "WHEN (SELECT COUNT(*) FROM PreviousRun) >= 1 " +
//                "BEGIN " +
//                "SELECT RAISE(FAIL, 'Only one row allowed!'); " +
//                "END;");
//        // Initalizes table with default value.  Skips if row is already present.
//        try
//        {
//            ExecuteSQL("INSERT OR IGNORE INTO PreviousRun (" +
//                "ID,LastRun) " +
//                "VALUES (0,'12/30/2018 00:00:00 AM');");
//        }
//        catch
//        {
//            logger.Debug("PreviousRun already populated.");
//        }
//    }
//    
//    private void MigrateTables()
//    {
//        try
//        {
//            ExecuteSQL("INSERT INTO NewsletterData (" +
//                            "Filename," +
//                            "Title," +
//                            "Season," +
//                            "Episode," +
//                            "Overview," +
//                            "ItemID," +
//                            "PosterPath," +
//                            "Type) " + 
//                       "SELECT " +
//                            "Filename," +
//                            "Title," +
//                            "Season," +
//                            "Episode," +
//                            "SeriesOverview," +
//                            "ItemID," +
//                            "PosterPath," +
//                            "Type " +
//                            "FROM ArchiveData;");
//            ExecuteSQL("DROP TABLE IF EXISTS CurrRunData");
//            ExecuteSQL("DROP TABLE IF EXISTS CurrNewsletterData");
//            ExecuteSQL("DROP TABLE IF EXISTS ArchiveData");
//            logger.Debug("Legacy tables successfully migrated.");
//        }
//        catch
//        {
//            logger.Debug("Legacy tables aren't present.");
//        }
//    }
//
//    public IEnumerable<IReadOnlyList<ResultSetValue>> Query(string query)
//    {
//        logger.Debug("Running Query: " + query);
//        return _db.Query(query);
//    }
//
//    public void ExecuteSQL(string query)
//    {
//        logger.Debug("Executing SQL Statement: " + query);
//        _db.Execute(query);
//    }
//
//    public void CloseConnection()
//    {
//        if (File.Exists(dbLockPath)) // Database is locked
//        {
//            logger.Debug("Disposing DB connection: " + dbFilePath);
//            _db!.Dispose();
//            logger.Debug("Removing database lock file: " + dbLockPath);
//            File.Delete(dbLockPath);
//        }
//        else
//        {
//            logger.Debug("Database lock file does not exist. Database is not use: " + dbLockPath);
//        }
//    }
//}