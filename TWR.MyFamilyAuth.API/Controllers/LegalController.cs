using Markdig;
using Microsoft.AspNetCore.Mvc;

namespace TWR.MyFamilyAuth.API.Controllers;

[ApiController]
[Route("api/legal")]
public class LegalController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public LegalController(IWebHostEnvironment env) => _env = env;

    [HttpGet("privacy-policy")]
    public IActionResult GetPrivacyPolicy()
    {
        var path = Path.Combine(_env.ContentRootPath, "Legal", "PrivacyPolicy.md");
        if (!System.IO.File.Exists(path))
            return NotFound("Privacy policy document not found.");

        var markdown = System.IO.File.ReadAllText(path);
        var html     = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        return Content(html, "text/html");
    }
}
