using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Blog;
using Realtorist.Models.Pagination;
using Realtorist.Web.Helpers;
using System;
using System.Net;
using System.Threading.Tasks;
using WilderMinds.RssSyndication;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Models.Helpers;

namespace Realtorist.Web.Application.Controllers
{
    [Route("blog")]
    public class BlogController : Controller
    {
        private const int DefaultBlogPostsLimit = 5;

        private readonly IBlogDataAccess _blogDataAccess;
        private readonly ISettingsProvider _settingsProvider;

        public BlogController(IBlogDataAccess blogDataAccess, ISettingsProvider settingsProvider)
        {
            _blogDataAccess = blogDataAccess ?? throw new ArgumentNullException(nameof(blogDataAccess));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        [HttpGet("")]
        public async Task<IActionResult> IndexAsync([FromQuery] PaginationRequest request)
        {
            if (request.Limit > 10) request.Limit = DefaultBlogPostsLimit;

            var posts = await _blogDataAccess.GetPostsAsync(request);
            return View(posts);
        }

        [HttpGet("tag/{tag}")]
        public async Task<IActionResult> TagAsync([FromRoute] string tag, [FromQuery] PaginationRequest request)
        {
            if (request.Limit > 10) request.Limit = DefaultBlogPostsLimit;

            var posts = await _blogDataAccess.GetPostsByTagAsync(request, tag);
            return View((tag, posts));
        }

        [HttpGet("category/{category}")]
        public async Task<IActionResult> CategoryAsync([FromRoute] string category, [FromQuery] PaginationRequest request)
        {
            if (request.Limit > 10) request.Limit = DefaultBlogPostsLimit;

            var posts = await _blogDataAccess.GetCategoryPostsAsync(request, category);
            return View((category, posts));
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchAsync([FromQuery] string query, [FromQuery] PaginationRequest request)
        {
            if (request.Limit > 10) request.Limit = DefaultBlogPostsLimit;

            var posts = await _blogDataAccess.SearchPostsAsync(request, query);
            return View((query, posts));
        }

        [HttpGet("{*link}", Order = 1)]
        public async Task<IActionResult> PostAsync([FromRoute] string link)
        {
            var post = await _blogDataAccess.GetPostAsync(link);
            
            await _blogDataAccess.IncrementPostViews(post.Id);
            return View(post);
        }

        [HttpPost("{postId}/comments")]
        public async Task<IActionResult> AddCommentAsync([FromRoute] Guid postId, [FromBody] Comment comment)
        {
            if (!TryValidateModel(comment)) return BadRequest();

            comment.Date = DateTime.UtcNow;

            var commentId = await _blogDataAccess.AddCommentAsync(postId, comment);

            return Request.IsAjaxRequest() ? Ok(commentId) : RedirectToAction("Post", new { link = (await _blogDataAccess.GetPostAsync(postId)).Link });
        }

        [HttpGet("rss")]
        public async Task<IActionResult> GetRssFeed()
        {
            var posts = await _blogDataAccess.GetPostsAsync(new PaginationRequest(1, DefaultBlogPostsLimit));
            var settings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            var profile = await _settingsProvider.GetSettingAsync<ProfileSettings>(SettingTypes.Profile);

            var feed = new Feed()
            {
                Title = settings.WebsiteName,
                Description = $"{settings.WebsiteName} - ${settings.WebsiteTitle}",
                Link = new Uri(HttpContext.Request.GetDisplayUrl()),
                Copyright = "(c) " + DateTime.Now.Year
            };

            foreach (var post in posts.Results)
            {
                var link = Url.Action("Post", "Blog", new { link = post.Link }, Request.Scheme, Request.Host.ToString());
                var text = $"<div><h1>{post.Title}</h1></div><div><h2>{post.SubTitle}</h2></div><div><img src=\"{post.Image}\"/></div><div>{post.Text.TruncateHtml(Constants.AverageCharactersPerSummarry)}</div>";

                var item = new Item
                {
                    Title = post.Title,
                    Body = text.HtmlToPlainText(),
                    FullHtmlContent = text,
                    Link = new Uri(link),
                    Permalink = link,
                    PublishDate = settings.GetDateTimeInTimeZoneFromUtc(post.PublishDate),
                    Author = new Author { Name = $"{profile.FirstName} {profile.LastName}", Email = profile.Email }
                };

                item.Categories.Add(post.Category);

                item.Comments = new Uri($"{link}#comments");

                feed.Items.Add(item);
            }

            var rss = feed.Serialize();

            return new ContentResult
            {
                Content = rss,
                ContentType = "application/rss+xml",
                StatusCode = (int)HttpStatusCode.OK
            };
        }

        [HttpGet("permalink/{postId}/{commentId}")]
        public async Task<IActionResult> PermalinkAsync(Guid postId, Guid? commentId)
        {
            var post = await _blogDataAccess.GetPostAsync(postId);
            var url = Url.Action("Post", new { link = post.Link});

            if (commentId is not null)
            {
                url += $"#comment-{commentId}";
            }

            return Redirect(url);
        }
    }
}
