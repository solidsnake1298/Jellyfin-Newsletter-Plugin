#pragma warning disable 1591, CA1304
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Newsletters.Configuration;
using Jellyfin.Plugin.Newsletters.Shared.Efcore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;

namespace Jellyfin.Plugin.Newsletters.Shared.Efcore;

//private string dbFilePath = Plugin.Instance.Configuration.DataPath + "/newsletters.db";

public class NLPContext : DbContext
{
    //private readonly PluginConfiguration config = Plugin.Instance.Configuration;
    //private string dbFilePath;
    //private string dbFilePath = config.DataPath + "/newsletters.db";
    private string connectionString;

    public NLPContext()
    {
        string dbFilePath = Plugin.Instance!.Configuration.DataPath + "/newsletters.db";
        connectionString = $"Data Source={dbFilePath}";
    }

    public DbSet<Newsletter> NewsletterData { get; set; }
    
    public DbSet<Previous> PreviousRun { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(connectionString);
    }
}

public class Newsletter
{
    [Key]
    required public string Filename { get; set; }

    required public string Title { get; set; }

    required public string Album { get; set; }

    required public int Season { get; set; }

    required public int Episode { get; set; }

    required public string Overview { get; set; }

    required public string ItemID { get; set; }

    required public string PosterPath { get; set; }

    required public string Type { get; set; }

    required public int Emailed { get; set; }
}

public class Previous
{
    [Key]
    required public int ID { get; set; }

    required public string LastRun { get; set; }
}