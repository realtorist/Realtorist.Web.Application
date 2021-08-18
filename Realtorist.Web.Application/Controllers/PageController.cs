using Microsoft.AspNetCore.Mvc;
using Realtorist.DataAccess.Abstractions;
using System.Threading.Tasks;

namespace Realtorist.Web.Application.Controllers
{
    [Route("pages")]
    public class PageController : Controller
    {
        private readonly IPagesDataAccess _pagesDataAccess;

        public PageController(IPagesDataAccess pagesDataAccess)
        {
            _pagesDataAccess = pagesDataAccess;
        }

        [HttpGet("{*link}", Order = 1)]
        public async Task<IActionResult> PageAsync([FromRoute] string link)
        {
            var page = await _pagesDataAccess.GetPageAsync(link);
            if (page.UnPublished)
            {
                return NotFound();
            }

            await _pagesDataAccess.IncrementPagetViews(page.Id);
            return View(page);
        }
    }
}
