using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Blog;
using Realtorist.Models.Pagination;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Helpers;
using Realtorist.Web.Models.Admin.Blog;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to blogs
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/blog")]
    [RequireAuthorization]
    public class BlogApiController : Controller
    {
        private readonly IBlogDataAccess _blogDataAccess;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IMapper _mapper;

        public BlogApiController(IBlogDataAccess blogDataAccess, ISettingsProvider settingsProvider, IMapper mapper)
        {
            _blogDataAccess = blogDataAccess ?? throw new ArgumentNullException(nameof(blogDataAccess));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        /// <summary>
        /// Gets blog posts
        /// </summary>
        /// <param name="request">Pagination request</param>
        /// <returns>Blog posts</returns>
        [Route("posts")]
        [HttpGet]
        public async Task<PaginationResult<PostListModel>> GetPostsAsync([FromQuery] PaginationRequest request)
        {
            var posts = await _blogDataAccess.GetPostsAsync<PostListModel>(request, true);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);

            foreach( var post in posts.Results)
            {
                post.PublishDate = websiteSettings.GetDateTimeInTimeZoneFromUtc(post.PublishDate);
            }

            return posts;
        }

        /// <summary>
        /// Gets blog post by id
        /// </summary>
        /// <param name="postId">Id of the post</param>
        /// <returns>Blog post</returns>
        [Route("posts/{postId}")]
        [HttpGet]
        public async Task<PostDetailsModel> GetPostAsync([FromRoute] Guid postId)
        {
            var post = await _blogDataAccess.GetPostAsync(postId);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            post.PublishDate = websiteSettings.GetDateTimeInTimeZoneFromUtc(post.PublishDate);

            return _mapper.Map<PostDetailsModel>(post);
        }

        /// <summary>
        /// Updates blog post by id
        /// </summary>
        /// <param name="postId">Id of the post to update</param>
        /// <param name="post">Post update model</param>
        /// <returns>Status code</returns>
        [Route("posts/{postId}")]
        [HttpPost]
        public async Task<IActionResult> UpdatePostAsync([FromRoute] Guid postId, [FromBody] PostUpdateModel post)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetModelStateValidationErrors());
            }

            if (await _blogDataAccess.IsLinkUsed(post.Link, new[] { postId }))
            {
                return BadRequest(new { link = "Link is already in use" });
            }

            // var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            // post.PublishDate = websiteSettings.GetDateTimeInUtcFromTimeZone(post.PublishDate);
            
            await _blogDataAccess.UpdatePostAsync(postId, post);
            return NoContent();
        }

        /// <summary>
        /// Creates new blog post
        /// </summary>
        /// <param name="post">New post</param>
        /// <returns>Post Id</returns>
        [Route("posts")]
        [HttpPut]
        public async Task<ActionResult<Guid>> CreatePostAsync([FromBody] PostUpdateModel post)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetModelStateValidationErrors());
            }

            if (await _blogDataAccess.IsLinkUsed(post.Link))
            {
                return BadRequest(new { link = "Link is already in use" });
            }

            // var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            // post.PublishDate = websiteSettings.GetDateTimeInUtcFromTimeZone(post.PublishDate);
            
            var id = await _blogDataAccess.AddPostAsync(post);
            return new ObjectResult(id);
        }

        /// <summary>
        /// Deletes blog post by id
        /// </summary>
        /// <param name="postId">Id of the post</param>
        /// <returns>No content</returns>
        [Route("posts/{postId}")]
        [HttpDelete]
        public async Task<NoContentResult> DeletePostAsync([FromRoute] Guid postId)
        {
            await _blogDataAccess.RemovePostAsync(postId);
            return NoContent();
        }

        /// <summary>
        /// Gets blog post comments
        /// </summary>
        /// <param name="postId">Id of the post</param>
        /// <param name="request">Pagination request</param>
        /// <returns>Blog post comments</returns>
        [Route("posts/{postId}/comments")]
        [HttpGet]
        public async Task<PaginationResult<CommentListModel>> GetPostCommentsAsync([FromRoute] Guid postId, [FromQuery] PaginationRequest request)
        {
            var comments = await _blogDataAccess.GetPostCommentsAsync(postId, request);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            foreach (var comment in comments.Results)
            {
                comment.Date = websiteSettings.GetDateTimeInTimeZoneFromUtc(comment.Date);
            }

            return comments;
        }

        /// <summary>
        /// Gets all blog comments
        /// </summary>
        /// <param name="request">Pagination request</param>
        /// <returns>All blog comments</returns>
        [Route("comments")]
        [HttpGet]
        public async Task<PaginationResult<CommentListModel>> GetCommentsAsync([FromQuery] PaginationRequest request)
        {
            var comments = await _blogDataAccess.GetCommentsAsync(request);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            foreach(var comment in comments.Results)
            {
                comment.Date = websiteSettings.GetDateTimeInTimeZoneFromUtc(comment.Date);
            }

            return comments;
        }

        /// <summary>
        /// Deletes blog post comment by id
        /// </summary>
        /// <param name="postId">Id of the post</param>
        /// <param name="commentId">Id of the comment</param>
        /// <returns>No content</returns>
        [Route("posts/{postId}/comments/{commentId}")]
        [HttpDelete]
        public async Task<NoContentResult> DeleteCommentAsync([FromRoute] Guid postId, [FromRoute] Guid commentId)
        {
            await _blogDataAccess.RemoveCommentAsync(postId, commentId);
            return NoContent();
        }

        /// <summary>
        /// Gets list of categories
        /// </summary>
        /// <returns>Categories</returns>
        [Route("categories")]
        [HttpGet]
        public async Task<IEnumerable<string>> GetCategories()
        {
            var result = await _blogDataAccess.GetCategoriesAsync(true);
            return result.Keys;
        }

        /// <summary>
        /// Gets list of tags
        /// </summary>
        /// <returns>Tags</returns>
        [Route("tags")]
        [HttpGet]
        public async Task<IEnumerable<string>> GetTags()
        {
            return await _blogDataAccess.GetTagsAsync(true);
        }

        /// <summary>
        /// Checks wheter the link is already in use
        /// </summary>
        /// <param name="link"Link to check></param>
        /// <param name="idsToExclude">List of page ids to exclude</param>
        /// <returns>Whether the link is already in use</returns>
        [Route("posts/link/check")]
        [HttpPost]
        public async Task<bool> IsLinkUsed([FromQuery] string link, [FromBody] Guid[] idsToExclude)
        {
            return await _blogDataAccess.IsLinkUsed(link, idsToExclude);
        }
    }
}