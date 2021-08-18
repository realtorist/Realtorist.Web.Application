using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Realtorist.Web.Application.Jobs.Background
{
    /// <summary>
    /// Describes background task queue
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        /// <summary>
        /// Enqueues the given task
        /// </summary>
        /// <param name="task">Task to enqueue</param>
        void EnqueueTask(Func<IServiceScopeFactory, CancellationToken, Task> task);

        /// <summary>
        /// Dequeues and returns one task. This method blocks until a task becomes available.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task to execute</returns>
        Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }

}