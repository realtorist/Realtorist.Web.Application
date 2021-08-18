using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Helpers;
using Realtorist.Web.Models.Admin.Links;
using Realtorist.Web.Models.Listings;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to links
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/links")]
    [RequireAuthorization]
    public class LinksApiController : Controller
    {
        private readonly IListingsDataAccess _listingsDataAccess;
        private readonly IBlogDataAccess _blogDataAccess;
        private readonly IPagesDataAccess _pagesDataAccess;

        private readonly ILogger _logger;

        public LinksApiController(IListingsDataAccess listingsDataAccess, IBlogDataAccess blogDataAccess, IPagesDataAccess pagesDataAccess, ILogger<LinksApiController> logger)
        {
            _listingsDataAccess = listingsDataAccess ?? throw new ArgumentNullException(nameof(listingsDataAccess));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blogDataAccess = blogDataAccess ?? throw new ArgumentNullException(nameof(blogDataAccess));
            _pagesDataAccess = pagesDataAccess ?? throw new ArgumentNullException(nameof(pagesDataAccess));
        }

        /// <summary>
        /// Gets all links available in the system
        /// </summary>
        /// <returns></returns>
        [Route("")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IEnumerable<SiteLink>> GetAllLinksAsync()
        {
            var nodes = new List<SiteLink>
            {
                new SiteLink(Url.Action("Index","Home"), "Home page"),
                new SiteLink(Url.Action("Contact","Home"), "Contact"),
                new SiteLink(Url.Action("HomeWorth","Home"), "How much is my home worth"),
                new SiteLink(Url.Action("Search","Property"), "Listings search"),
                new SiteLink(Url.Action("Index","Blog"), "Blog")
            };

            var pages = await _pagesDataAccess.GetPagesAsync();
            nodes.AddRange(pages.Select(p => new SiteLink(Url.Action("Page", "Page", new { link = p.Link }), $"Page: {p.Title}")));

            var blogPosts = await _blogDataAccess.GetPostsAsync();
            nodes.AddRange(blogPosts.Select(p => new SiteLink(Url.Action("Post", "Blog", new { link = p.Link }), $"Blog post: {p.Title}")));

            var blogCategories = await _blogDataAccess.GetCategoriesAsync();
            nodes.AddRange(blogCategories.Select(c => new SiteLink(Url.Action("Category", "Blog", new { category = c.Key }), $"Blog category: {c.Key}")));

            var blogTags = await _blogDataAccess.GetTagsAsync();
            nodes.AddRange(blogTags.Select(t => new SiteLink(Url.Action("Tag", "Blog", new { tag = t }), $"Blog tag: {t}")));

            var listings = await _listingsDataAccess.GetAllListingsAsync<ListingLinkModel>();
            nodes.AddRange(listings.Select(l => new SiteLink(Url.Action("Details", "Property", new { id = l.Id, address = l.Address.GetAsUrlParameter() }), $"Listing: {l.Address.StreetAddress} {l.Address.City}")));

            return nodes;
        }
    }
}