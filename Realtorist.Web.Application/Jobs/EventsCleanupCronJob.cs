using System;
using System.Threading;
using System.Threading.Tasks;
using EasyCronJob.Abstractions;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Events;
using Realtorist.Services.Abstractions.Events;

namespace Realtorist.Web.Application.Jobs
{
    public class EventsCleanupCronJob : CronJobService
    {
        private readonly IEventsDataAccess _eventsDataAccess;
        private readonly IEventLogger _eventLogger;
        private readonly ILogger _logger;

        public EventsCleanupCronJob(
            ICronConfiguration<EventsCleanupCronJob> cronConfiguration,
            IEventsDataAccess eventsDataAccess,
            IEventLogger eventLogger,
            ILogger<EventsCleanupCronJob> logger)
            : base(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventsDataAccess = eventsDataAccess ?? throw new ArgumentNullException(nameof(eventsDataAccess));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        public override async Task DoWork(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting update flow");

            try
            {
                var maxDate = DateTime.UtcNow.AddMonths(-3);
                var result = await _eventsDataAccess.DeleteOldEventsAsync(maxDate);

                _logger.LogInformation($"Successfully removed {result} old events older than {maxDate}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to clean up old events");
                await _eventLogger.CreateEventAsync(EventTypes.Generic, "An error has occured during events cleanup", "An exception occured during cleaning up events", e);
            }

            await base.DoWork(cancellationToken);
        }
    }
}