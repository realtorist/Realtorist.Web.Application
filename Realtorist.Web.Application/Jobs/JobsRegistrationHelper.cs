using System;
using EasyCronJob.Core;
using Microsoft.Extensions.DependencyInjection;
using Realtorist.Models.Helpers;
using Realtorist.Models.Settings;

namespace Realtorist.Web.Application.Jobs
{
    public static class JobsRegistrationHelper 
    {
        public static void RegisterJobs(this IServiceCollection services, WebsiteSettings websiteSettings)
        {
            var timezone = !websiteSettings.Timezone.IsNullOrEmpty() ? TimeZoneInfo.FindSystemTimeZoneById(websiteSettings.Timezone) : TimeZoneInfo.Utc;
            services.ApplyResulation<ListingUpdateFlowCronJob>(options =>
            {
                options.CronExpression = "* * * * *";
                options.TimeZoneInfo = timezone;
            });

            services.ApplyResulation<EventsCleanupCronJob>(options =>
            {
                options.CronExpression = "0 0 * * *";
                options.TimeZoneInfo = timezone;
            });
        }
    }
}