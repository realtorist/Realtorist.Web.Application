using GoogleAnalyticsTracker.AspNet;
using GoogleAnalyticsTracker.Simple;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Realtorist.Web.Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly IListingsDataAccess _listingsDataAccess;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;

        public AnalyticsController(
            IListingsDataAccess listingsDataAccess,
            ISettingsProvider settingsProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<AnalyticsController> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _listingsDataAccess = listingsDataAccess ?? throw new ArgumentNullException(nameof(listingsDataAccess));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        [HttpGet("")]
        public async Task<IActionResult> Event(EventType type, Guid? listingId)
        {
            var analyticsSettings = await _settingsProvider.GetSettingAsync<AnalyticsSettings>(SettingTypes.Analytics);
            var retsSettings = await _settingsProvider.GetSettingAsync<ListingsSettings>(SettingTypes.Listings);

            if (!string.IsNullOrEmpty(analyticsSettings?.GoogleAnalyticsId))
            {
                using (var tracker = new SimpleTracker(analyticsSettings.GoogleAnalyticsId, new AspNetCoreTrackerEnvironment()))
                {
                    var trackerResult = await tracker.TrackEventAsync("Website", type.ToString(), string.Empty, new Dictionary<int, string>());
                    if (!trackerResult.Success)
                    {
                        _logger.LogWarning($"Failed to submit event {type} to Google Analytics: {trackerResult.Exception}");
                    }
                }
            }

            if (listingId is not null)
            {
                var listing = await _listingsDataAccess.GetListingAsync(listingId.Value);

                if (listing.FeedId is not null)
                {
                    var creaConfiguration = retsSettings?.Feeds?.First(x => x.Id == listing.FeedId);
                    if (creaConfiguration is not null && creaConfiguration.FeedType == "CREA")
                    {
                        using (var client = _httpClientFactory.CreateClient())
                        {
                            var ddfEventType = type switch
                            {
                                EventType.View => "view",
                                EventType.Form => "email_realtor",
                                _ => throw new NotImplementedException()
                            };

                            var uuid = HttpContext.TraceIdentifier;

                            var url = $"https://analytics.crea.ca/LogEvents.svc/LogEvents?ListingID={listing.ExternalId}&EventType={ddfEventType}&UUID={uuid}&DestinationID={creaConfiguration.DestinationId}";
                            await client.GetAsync(url);
                        }
                    }
                }
            }

            return Ok();
        }
    }
}
