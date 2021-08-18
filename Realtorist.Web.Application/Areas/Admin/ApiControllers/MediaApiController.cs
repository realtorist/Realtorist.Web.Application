using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Realtorist.Models.Media;
using Realtorist.Models.Pagination;
using Realtorist.Services.Abstractions.Upload;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Models.Upload;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations for the media files
    /// </summary>
    [Area("admin")]
    [Route("api/admin/media")]
    [RequireAuthorization]
    public class MediaApiController : Controller 
    {
        private readonly IUploadService _uploadService;

        public MediaApiController(IUploadService uploadService)
        {
            _uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        }

        /// <summary>
        /// Gets all uploads
        /// </summary>
        /// <param name="request">Pagination request</param>
        /// <returns>All uploads</returns>
        [Route("")]
        [HttpGet]
        public async Task<PaginationResult<MediaFile>> GetUploadsAsync([FromQuery] PaginationRequest request)
        {
            return await _uploadService.GetFilesAsync(request);
        }

        /// <summary>
        /// Uploads new files
        /// </summary>
        /// <returns>Urls to uploaded files/returns>
        [Route("")]
        [HttpPost]
        public async Task<MediaFile[]> Upload()
        {
            var files = this.Request.Form.Files;
            var results = new List<MediaFile>();
            foreach (var file in files)
            {
                using (var fileStream = file.OpenReadStream()) 
                {
                    results.Add(await _uploadService.UploadFileAsync(file.OpenReadStream(), file.FileName));
                }
            }

            return results.ToArray();    
        }

        /// <summary>
        /// Uploads a file from the editor
        /// </summary>
        /// <returns>Upload result</returns>
        [Route("editor")]
        [HttpPost]
        public async Task<EditorUploadResponse> EditorUpload()
        {
            var files = this.Request.Form.Files;
            var response = new List<EditorUploadResponseFile>();
            foreach (var file in files)
            {
                using (var fileStream = file.OpenReadStream()) 
                {
                    var mediaFile = await _uploadService.UploadFileAsync(file.OpenReadStream(), file.FileName);
                    response.Add(new EditorUploadResponseFile {
                        Name = mediaFile.Name,
                        Url = mediaFile.Url,
                        Size = mediaFile.Size
                    });
                }
            }

            return new EditorUploadResponse {
                Result = response.ToArray()
            };
        }

        /// <summary>
        /// Gets images for the gallery in editor
        /// </summary>
        /// <returns>Gallery result</returns>
        [Route("editor/gallery")]
        [HttpGet]
        public async Task<EditorImageGalleryResponse> EditorGallery()
        {
            var files = await _uploadService.GetFilesAsync();
            return new EditorImageGalleryResponse {
                Result = files.Select(mediaFile => 
                    new EditorImageGalleryResponse.EditorImageGalleryResponseFile { 
                        Src = mediaFile.Url
                    }).ToArray()
            };
        }

        [Route("{id}")]
        [HttpDelete]
        public async Task<NoContentResult> DeleteFile([FromRoute] string id) 
        {
            await _uploadService.DeleteFileAsync(id);
            return NoContent();
        }
    }
}