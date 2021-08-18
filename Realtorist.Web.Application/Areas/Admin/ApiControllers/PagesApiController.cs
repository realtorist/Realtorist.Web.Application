using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Page;
using Realtorist.Models.Pagination;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Helpers;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to pages
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/pages")]
    [RequireAuthorization]
    public class PagesApiController : Controller
    {
        private readonly IPagesDataAccess _pagesDataAccess;
        private readonly IMapper _mapper;

        public PagesApiController(IPagesDataAccess pagesDataAccess, IMapper mapper)
        {
            _pagesDataAccess = pagesDataAccess ?? throw new ArgumentNullException(nameof(pagesDataAccess));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// Gets pages
        /// </summary>
        /// <param name="request">Pagination request</param>
        /// <returns>Pages</returns>
        [Route("")]
        [HttpGet]
        public async Task<PaginationResult<PageListModel>> GetPostsAsync([FromQuery] PaginationRequest request)
        {
            return await _pagesDataAccess.GetPagesAsync<PageListModel>(request, true);
        }

        /// <summary>
        /// Gets page by id
        /// </summary>
        /// <param name="pageId">Id of the post</param>
        /// <returns>Page</returns>
        [Route("{pageId}")]
        [HttpGet]
        public async Task<PageUpdateModel> GetPostAsync([FromRoute] Guid pageId)
        {
            var post = await _pagesDataAccess.GetPageAsync(pageId);
            return _mapper.Map<PageUpdateModel>(post);
        }

        /// <summary>
        /// Updates page by id
        /// </summary>
        /// <param name="pageId">Id of the page to update</param>
        /// <param name="page">Page new update model</param>
        /// <returns>Status code</returns>
        [Route("{pageId}")]
        [HttpPost]
        public async Task<IActionResult> UpdatePageAsync([FromRoute] Guid pageId, [FromBody] PageUpdateModel page)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetModelStateValidationErrors());
            }

            if (await _pagesDataAccess.IsLinkUsed(page.Link, new[] { pageId }))
            {
                return BadRequest(new { link = "Link is already in use" });
            }
            
            await _pagesDataAccess.UpdatePageAsync(pageId, page);
            return NoContent();
        }

        /// <summary>
        /// Creates new page
        /// </summary>
        /// <param name="page">New page</param>
        /// <returns>Page Id</returns>
        [Route("")]
        [HttpPut]
        public async Task<ActionResult<Guid>> CreatePageAsync([FromBody] PageUpdateModel page)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetModelStateValidationErrors());
            }

            if (await _pagesDataAccess.IsLinkUsed(page.Link))
            {
                return BadRequest(new { link = "Link is already in use" });
            }
            
            var id = await _pagesDataAccess.AddPageAsync(page);
            return new ObjectResult(id);
        }

        /// <summary>
        /// Deletes page by id
        /// </summary>
        /// <param name="pageId">Id of the page</param>
        /// <returns>No content</returns>
        [Route("{pageId}")]
        [HttpDelete]
        public async Task<NoContentResult> DeletePostAsync([FromRoute] Guid pageId)
        {
            await _pagesDataAccess.RemovePageAsync(pageId);
            return NoContent();
        }

        /// <summary>
        /// Checks wheter the link is already in use
        /// </summary>
        /// <param name="link"Link to check></param>
        /// <param name="idsToExclude">List of page ids to exclude</param>
        /// <returns>Whether the link is already in use</returns>
        [Route("link/check")]
        [HttpPost]
        public async Task<bool> IsLinkUsed([FromQuery] string link, [FromBody] Guid[] idsToExclude)
        {
            return await _pagesDataAccess.IsLinkUsed(link, idsToExclude);
        }
    }
}