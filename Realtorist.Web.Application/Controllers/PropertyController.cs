using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.GeoCoding.Abstractions;
using Realtorist.Models.Helpers;
using Realtorist.Models.Geo;
using Realtorist.Models.Pagination;
using Realtorist.Models.Search;
using Realtorist.Web.Helpers;
using Realtorist.Web.Models.Enums;
using Realtorist.Web.Models.Listings;
using System;
using System.Threading.Tasks;

namespace Realtorist.Web.Application.Controllers
{
    [Route("property")]
    public class PropertyController : Controller
    {
        private readonly IListingsDataAccess _listingsDataAccess;
        private readonly IGeoCoder _geoCoder;
        private readonly ViewRenderService  _viewToStringRenderer;
        private readonly ILogger _logger;

        public PropertyController(IListingsDataAccess listingsDataAccess, IGeoCoder geoCoder, ViewRenderService  viewToStringRenderer, ILogger<PropertyController> logger)
        {
            _listingsDataAccess = listingsDataAccess ?? throw new ArgumentNullException(nameof(listingsDataAccess));
            _geoCoder = geoCoder ?? throw new ArgumentNullException(nameof(geoCoder));
            _viewToStringRenderer = viewToStringRenderer ?? throw new ArgumentNullException(nameof(viewToStringRenderer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Details([FromRoute] Guid id)
        {
            var listing = await _listingsDataAccess.GetListingAsync(id);
            
            if (listing.Address is not null)
            {
                return RedirectToActionPermanent(nameof(Details), new { id = id, address = listing.Address.GetAsUrlParameter() });
            }

            return View(listing);
        }

        [HttpGet("{id}/{address}")]
        public async Task<IActionResult> Details([FromRoute] Guid id, [FromRoute] string address)
        {
            var listing = await _listingsDataAccess.GetListingAsync(id);
            await _listingsDataAccess.IncrementListingViews(id);
            
            return View(listing);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(ListingSearchRequest request, ListingSearchRequestType requestType = ListingSearchRequestType.Default)
        {
            if (request == null) request = new ListingSearchRequest();
            if (request.Pagination == null) request.Pagination = new PaginationRequest(1, Constants.DefaultPaginationLimit);
            if (request.Pagination.Limit == 0) request.Pagination.Limit = Constants.DefaultPaginationLimit;

            if (request.Pagination.Page < 1 || request.Pagination.Limit < 1) return BadRequest();

            if (!string.IsNullOrEmpty(request.Address) && request.Boundaries is null)
            {
                var coordinates = await _geoCoder.GetCoordinatesAsync(request.Address);
                request.Boundaries = CoordinateBoundaries.FromCenterAndDistanceToCorner(coordinates, Constants.DefaultMapRadiusMeters * Math.Sqrt(2));
            }

            var model = await _listingsDataAccess.SearchAsync(request);

            switch (requestType) {
                case ListingSearchRequestType.Default:
                    return View(model);

                case ListingSearchRequestType.Partial:
                    return PartialView("_SearchResultsWithPagination", model);

                case ListingSearchRequestType.Api:
                    var view = await _viewToStringRenderer.RenderToStringAsync("_SearchResultsWithPagination", model, true);
                    var result = new ListingsSearchApiResult
                    {
                        Coordinates = model.Coordinates,
                        View = view
                    };

                    return Json(result);

                default:
                    throw new InvalidOperationException($"Unknown request type: {requestType}");
            }
        }

        [HttpGet("{listingId}/popup")]
        public async Task<IActionResult> GetPopupHtmlAsync([FromRoute] Guid listingId)
        {
            var listing = await _listingsDataAccess.GetListingAsync(listingId);
            return PartialView("_ListingItem", (listing, true));
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestionsAsync([FromQuery] string query = "", [FromQuery] int limit = 5)
        {
            var results = await _listingsDataAccess.GetListingSearchSuggestionsAsync(query, limit);
            return Json(results);
        }
    }
}