using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Dto;
using Realtorist.Web.Helpers;
using Realtorist.Web.Models;
using Realtorist.Web.Models.Listings;
using SimpleMvcSitemap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Realtorist.Web.Application.Controllers
{
    [Route("home")]
    public class HomeController : Controller
    {
        private readonly IListingsDataAccess _listingsDataAccess;
        private readonly IBlogDataAccess _blogDataAccess;
        private readonly IPagesDataAccess _pagesDataAccess;
        
        private readonly ILogger<HomeController> _logger;

        public HomeController(IListingsDataAccess listingsDataAccess, IBlogDataAccess blogDataAccess, IPagesDataAccess pagesDataAccess, ILogger<HomeController> logger)
        {
            _listingsDataAccess = listingsDataAccess ?? throw new ArgumentNullException(nameof(listingsDataAccess));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _blogDataAccess = blogDataAccess ?? throw new ArgumentNullException(nameof(blogDataAccess));
            _pagesDataAccess = pagesDataAccess ?? throw new ArgumentNullException(nameof(pagesDataAccess));
        }

        [Route("/")]
        [Route("", Order = 1)]
        public async Task<IActionResult> IndexAsync()
        {
            return View();
        }

        [Route("contact")]
        public IActionResult Contact()
        {
            return View(new RequestInformationModel());
        }

        [Route("/home-worth")]
        public IActionResult HomeWorth()
        {
            return View(new RequestInformationModel());
        }

        [Route("/oh-no")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [Route("/sitemap.xml")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> SitemapAsync()
        {
            var nodes = new List<SitemapNode>
            {
                new SitemapNode(Url.Action("Index","Home")),
                new SitemapNode(Url.Action("Contact","Home")),
                new SitemapNode(Url.Action("HomeWorth","Home")),
                new SitemapNode(Url.Action("Search","Property")),
                new SitemapNode(Url.Action("Index","Blog")),
            };

            var pages = await _pagesDataAccess.GetPagesAsync();
            nodes.AddRange(pages.Select(p => new SitemapNode(Url.Action("Page", "Page", new { link = p.Link }))));

            var blogPosts = await _blogDataAccess.GetPostsAsync();
            nodes.AddRange(blogPosts.Select(p => new SitemapNode(Url.Action("Post", "Blog", new { link = p.Link }))));

            var blogCategories = await _blogDataAccess.GetCategoriesAsync();
            nodes.AddRange(blogCategories.Select(c => new SitemapNode(Url.Action("Category", "Blog", new { category = c.Key }))));

            var blogTags = await _blogDataAccess.GetTagsAsync();
            nodes.AddRange(blogTags.Select(t => new SitemapNode(Url.Action("Tag", "Blog", new { tag = t }))));

            var listings = await _listingsDataAccess.GetAllListingsAsync<ListingLinkModel>();
            nodes.AddRange(listings.Select(l => new SitemapNode(Url.Action("Details", "Property", new { id = l.Id, address = l.Address.GetAsUrlParameter() }))));

            return new SitemapProvider().CreateSitemap(new SitemapModel(nodes));
        }
    }
}
