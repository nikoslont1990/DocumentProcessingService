using DocumentProcessingService.Application.DTOs;
using DocumentProcessingService.Application.Interfaces;
using DocumentProcessingService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace DocumentProcessingService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly ILogger<DocumentsController> _logger;
        private const int MaxFileSize = 1 * 1024 * 1024;

        public DocumentsController(IDocumentService documentService, ILogger<DocumentsController> logger)
        {
            _documentService = documentService;
            _logger = logger;
        }

        [HttpGet("{documentId:guid}")]
        public  async Task<IActionResult> GetDocument(Guid documentId, CancellationToken cancellationToken)
        {
            try
            {
                Document?  document = await _documentService.GetById(documentId, cancellationToken);

                if (document == null)
                    return NotFound();

                CreateDocumentResponse response = new CreateDocumentResponse()
                {
                    FileName = document.FileName,
                    DocumentId = document.Id,
                    CreatedAt = DateTime.Now,
                    Status = document.Status.GetDisplayName().ToString(),
                };

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Validation error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving document");
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }


        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult> CreateDocument(IFormFile file, CancellationToken cancellationToken)
        {
            try
            {
                if (file is null || file.Length == 0)
                {
                    return BadRequest("A file is required.");
                }

                if (file.Length > MaxFileSize)
                {
                    return BadRequest("The maximum file size is 10 MB.");
                }

                using (var stream = file.OpenReadStream())
                {
                    var document = await _documentService.CreateDocument(
                        file.FileName,
                        file.Length,
                        file.ContentType,
                        stream,
                        cancellationToken);

                    if (document is null)
                        return StatusCode(500, new { error = "Failed to create document." });


                    var response = new CreateDocumentResponse
                    {
                        DocumentId = document.Id,
                        FileName = document.FileName,
                        Status = document.Status.GetDisplayName().ToString(),
                        CreatedAt = document.CreatedAt ?? DateTime.Now
                    };

                    return CreatedAtAction(nameof(GetDocument), new { documentId = document.Id }, response);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Validation error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving document");
                return StatusCode(500, new { error = "An unexpected error occurred" });
            }
        }


        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
        {
            var result = await _documentService.DownloadAsync(
                id,
                cancellationToken);

            if (result is null)
                return NotFound();

            return File(
                result.Content,
                result.ContentType,
                result.FileName);
        }
    }
}