using System;
using System.Threading;
using System.Threading.Tasks;
using EasyCronJob.Abstractions;
using Microsoft.Extensions.Logging;
using Realtorist.Models.Events;
using Realtorist.RetsClient.Abstractions;
using Realtorist.Services.Abstractions.Events;

namespace Realtorist.Web.Application.Jobs
{
    public class ListingUpdateFlowCronJob : CronJobService
    {
        private readonly IEventLogger _eventLogger;
        private readonly ILogger _logger;
        private readonly IUpdateFlow _updateFlow;

        public ListingUpdateFlowCronJob(
            ICronConfiguration<ListingUpdateFlowCronJob> cronConfiguration,
            IUpdateFlow updateFlow,
            IEventLogger eventLogger,
            ILogger<ListingUpdateFlowCronJob> logger)
            : base(cronConfiguration.CronExpression, cronConfiguration.TimeZoneInfo)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _updateFlow = updateFlow ?? throw new ArgumentNullException(nameof(updateFlow));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        public override async Task DoWork(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting update flow");

            try
            {
                await _updateFlow.LaunchAsync();
            }
            catch (Exception e)
            {
                const string LogMessage = "An error has occured during listings update";
                _logger.LogError(e, $"Failed to update listings");
                await _eventLogger.CreateEventAsync(EventTypes.ListingUpdate, LogMessage, LogMessage, e);
            }

            await base.DoWork(cancellationToken);
        }
    }
}