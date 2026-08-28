#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.NewslettersRedux.Scanner;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.NewslettersRedux.ScheduledTasks
{
    /// <summary>
    /// Class RefreshMediaLibraryTask.
    /// </summary>
    public class ScanLibraryTask : IScheduledTask
    {
        private readonly ILibraryManager libraryManager;
        private readonly IRecordingsManager recordingManager;
        private readonly IDtoService dtoService;

        public ScanLibraryTask(ILibraryManager libraryManager, IRecordingsManager recordingManager, IDtoService dtoService)
        {
            this.libraryManager = libraryManager;
            this.recordingManager = recordingManager;
            this.dtoService = dtoService;
        }

        /// <inheritdoc />
        public string Name => "Filesystem Scraper";

        /// <inheritdoc />
        public string Description => "Gather info on recently added media and store it for Newsletters";

        /// <inheritdoc />
        public string Category => "Newsletters";

        /// <inheritdoc />
        public string Key => "EmailNewsletters";

        /// <summary>
        /// Creates the triggers that define when the task will run.
        /// </summary>
        /// <returns>IEnumerable{BaseTaskTrigger}.</returns>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            yield return new TaskTriggerInfo
            {
                // Type = TaskTriggerInfo.Type,
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(1).Ticks
            };
        }

        /// <inheritdoc />
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(0);

            var myScraper = new Scraper(libraryManager, recordingManager, dtoService, progress);
            return myScraper.GetNewsletterData();
        }
    }
}