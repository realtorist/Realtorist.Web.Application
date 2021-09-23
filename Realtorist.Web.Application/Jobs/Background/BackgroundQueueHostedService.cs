using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Realtorist.Models.Events;
using Realtorist.Services.Abstractions.Events;
using Realtorist.Web.Models.Abstractions.Jobs.Background;

namespace Realtorist.Web.Application.Jobs.Background
{
    /// <summary>
    /// Hosted service for executing background tasks
    /// </summary>
    public class BackgroundQueueHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BackgroundQueueHostedService> _logger;
        private readonly IEventLogger _eventLogger;

        public BackgroundQueueHostedService(IBackgroundTaskQueue taskQueue, IServiceScopeFactory serviceScopeFactory, ILogger<BackgroundQueueHostedService> logger, IEventLogger eventLogger)
        {
            _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Dequeue and execute tasks until the application is stopped
            while (!stoppingToken.IsCancellationRequested)
            {
                // Get next task
                // This blocks until a task becomes available
                var task = await _taskQueue.DequeueAsync(stoppingToken);

                try
                {
                    // Run task
                    await task(_serviceScopeFactory, stoppingToken);
                }
                catch (Exception ex)
                {
                    const string LogMessage = "An error occured during execution of a background task";
                    _logger.LogError(ex, LogMessage);
                    await _eventLogger.CreateEventAsync(EventTypes.Generic, LogMessage, LogMessage, ex);
                }
            }
        }
    }
}