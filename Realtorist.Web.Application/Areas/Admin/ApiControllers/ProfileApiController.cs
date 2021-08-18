using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Events;
using Realtorist.Models.Helpers;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Events;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Models.Admin.User;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to user profile
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/profile")]
    [RequireAuthorization]
    public class ProfileApiController : Controller
    {
        private readonly ICustomerRequestsDataAccess _customerRequestsDataAccess;
        private readonly IEventLogger _eventLogger;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        public ProfileApiController(
            ISettingsProvider settingsProvider,
            ICustomerRequestsDataAccess customerRequestsDataAccess,
            IEventLogger eventLogger,
            IMapper mapper,
            ILogger<ProfileApiController> logger)
        {
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _customerRequestsDataAccess = customerRequestsDataAccess ?? throw new ArgumentNullException(nameof(customerRequestsDataAccess));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _logger = logger;
        }

        /// <summary>
        /// Gets user's profile
        /// </summary>
        /// <returns>User profile</returns>
        [Route("")]
        public async Task<UserProfile> GetProfile()
        {
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            var profileSettings = await _settingsProvider.GetSettingAsync<ProfileSettings>(SettingTypes.Profile);
            
            var profile = _mapper.Map(websiteSettings, _mapper.Map<UserProfile>(profileSettings));

            return profile;
        }

        /// <summary>
        /// Gets user's state
        /// </summary>
        /// <returns>User state</returns>
        [Route("state")]
        public async Task<UserState> GetState()
        {
            var unReadRequests = await _customerRequestsDataAccess.GetUnreadCustomerRequestsCountAsync();

            return new UserState
            {
                UnreadRequestsCount = unReadRequests
            };
        }

        /// <summary>
        /// Gets latests events using Server Side Events
        /// </summary>
        /// <returns>Latest events</returns>
        [Route("events/sse")]
        public async Task GetEventsSseAsync(CancellationToken cancellationToken)
        {
            var response = Response;
            response.Headers.Add("Content-Type", "text/event-stream");

            EventHandler<Event> onEventCreated = async (sender, eventArgs) =>
            {
                try
                {
                    await Response.WriteAsync($"data:{eventArgs.ToJson()}\n\n");
                    await Response.Body.FlushAsync();
                }
                catch (Exception e)
                {     
                    _logger.LogError(e, $"Failed to push event to Event source");               
                }
            };

            _eventLogger.EventCreated += onEventCreated;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000);
            }

            _eventLogger.EventCreated -= onEventCreated;
        }
    }
}