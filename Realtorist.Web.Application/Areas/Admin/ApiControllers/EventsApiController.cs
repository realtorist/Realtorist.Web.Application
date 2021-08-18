using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Events;
using Realtorist.Models.Helpers;
using Realtorist.Models.Pagination;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Helpers;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to pages
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/events")]
    [RequireAuthorization]
    public class EventsApiController : Controller
    {
        private readonly IEventsDataAccess _eventsDataAccess;
        private readonly ISettingsProvider _settingsProvider;

        public EventsApiController(IEventsDataAccess eventsDataAccess, ISettingsProvider settingsProvider)
        {
            _eventsDataAccess = eventsDataAccess ?? throw new ArgumentNullException(nameof(eventsDataAccess));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        /// <summary>
        /// Gets events
        /// </summary>
        /// <param name="request">Pagination request</param>
        /// <param name="filter">Search filters</param>
        /// <returns>Events</returns>
        [Route("")]
        [HttpGet]
        public async Task<PaginationResult<Event>> GetEventsAsync([FromQuery] PaginationRequest request, [ModelBinder(BinderType = typeof(QueryStringToDictionaryBinder))] Dictionary<string, string> filter)
        {
            if (request != null && request.SortField.IsNullOrEmpty())
            {
                request.SortField = nameof(Event.CreatedAt);
                request.SortOrder = Realtorist.Models.Enums.SortByOrder.Desc;
            }

            var events = await _eventsDataAccess.GetEventsAsync(request, filter);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            foreach (var @event in events.Results)
            {
                @event.CreatedAt = websiteSettings.GetDateTimeInTimeZoneFromUtc(@event.CreatedAt);
            }

            return events;
        }

        /// <summary>
        /// Deletes all events
        /// </summary>
        /// <returns>Number of events removed</returns>
        [Route("")]
        [HttpDelete]
        public async Task<long> DeleteAllEventsAsync()
        {
            var result = await _eventsDataAccess.DeleteAllEventsAsync();
            return result;
        }
    }
}