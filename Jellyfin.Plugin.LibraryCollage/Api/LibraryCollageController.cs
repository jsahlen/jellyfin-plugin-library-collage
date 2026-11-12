using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.LibraryCollage.Services;
using Jellyfin.Plugin.LibraryCollage.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.LibraryCollage.Api;

/// <summary>
/// Endpoints for the settings page.
/// </summary>
[ApiController]
[Route("LibraryCollage")]
[Authorize(Policy = Policies.RequiresElevation)]
public class LibraryCollageController : ControllerBase
{
    private readonly CollageService _collageService;
    private readonly ITaskManager _taskManager;
    private readonly CustomFontStore _fontStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryCollageController"/> class.
    /// </summary>
    /// <param name="collageService">The collage service.</param>
    /// <param name="taskManager">The task manager.</param>
    /// <param name="fontStore">The custom font store.</param>
    public LibraryCollageController(CollageService collageService, ITaskManager taskManager, CustomFontStore fontStore)
    {
        _collageService = collageService;
        _taskManager = taskManager;
        _fontStore = fontStore;
    }

    /// <summary>
    /// Lists all libraries with their settings and image status.
    /// </summary>
    /// <returns>The libraries.</returns>
    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryStatus>> GetLibraries() => Ok(_collageService.GetLibraries());

    /// <summary>
    /// Renders a preview with the saved settings, without changing the library image.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <returns>The JPEG.</returns>
    [HttpGet("Preview/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPreview([FromRoute] Guid libraryId)
    {
        var jpeg = _collageService.RenderPreview(libraryId);
        return jpeg is null ? NotFound() : File(jpeg, "image/jpeg");
    }

    /// <summary>
    /// Generates the collage for one library now.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened.</returns>
    [HttpPost("Generate/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateOne([FromRoute] Guid libraryId, CancellationToken cancellationToken)
    {
        var result = await _collageService.GenerateOneAsync(libraryId, cancellationToken).ConfigureAwait(false);
        return Ok(new { Result = result });
    }

    /// <summary>
    /// Starts the scheduled task now.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpPost("Generate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Generate()
    {
        _taskManager.QueueScheduledTask<GenerateCollagesTask>();
        return NoContent();
    }

    /// <summary>
    /// Gets the uploaded font.
    /// </summary>
    /// <returns>The font's family name, or <c>null</c> when the built-in fonts are used.</returns>
    [HttpGet("Font")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetFont() => Ok(new { FamilyName = _fontStore.GetFamilyName() });

    /// <summary>
    /// Uploads a font that replaces the built-in fonts of every text style.
    /// </summary>
    /// <param name="file">A TrueType or OpenType font file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The font's family name.</returns>
    [HttpPost("Font")]
    [RequestSizeLimit(CustomFontStore.MaxBytes + (1024 * 1024))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFont(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (file.Length > CustomFontStore.MaxBytes)
        {
            return BadRequest("The font file is larger than 20 MB.");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var familyName = _fontStore.Save(buffer.ToArray());
        return familyName is null
            ? BadRequest("The file is not a font that can be read. Use a .ttf or .otf file.")
            : Ok(new { FamilyName = familyName });
    }

    /// <summary>
    /// Removes the uploaded font so the built-in fonts are used again.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpDelete("Font")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult DeleteFont()
    {
        _fontStore.Remove();
        return NoContent();
    }
}
