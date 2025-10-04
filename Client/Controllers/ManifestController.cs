using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Client.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ManifestController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public ManifestController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        [Route("/manifest.json")]
        public IActionResult GetManifest()
        {
            var environment = _configuration["Environment"] ?? "Production";
            var version = _configuration["Version"] ?? "1.0.0";
            var isStaging = environment.Equals("Staging", StringComparison.OrdinalIgnoreCase);
            var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);

            var manifest = new
            {
                name = isDevelopment ? "WordScape (DEV)" : 
                       isStaging ? "WordScape (STAGING)" : 
                       "CalvinHsia WordScape Game",
                       
                short_name = isDevelopment ? "WordScape DEV" : 
                            isStaging ? "WordScape STG" : 
                            "WordScape",
                            
                version = isDevelopment ? $"{version}-dev" : 
                         isStaging ? $"{version}-staging" : 
                         version,
                         
                description = "Play WordScape - Find words in the crossword grid using the circle letters",
                start_url = "/wordscape",
                display = "standalone",
                background_color = isDevelopment ? "#fff3cd" : 
                                  isStaging ? "#fff3cd" : 
                                  "#ffffff",
                                  
                theme_color = isDevelopment ? "#dc3545" : 
                             isStaging ? "#ffc107" : 
                             "#007bff",
                             
                orientation = "portrait-primary",
                scope = "/",
                icons = new[]
                {
                    new
                    {
                        src = "icon.svg",
                        sizes = "any",
                        type = "image/svg+xml",
                        purpose = "any maskable"
                    }
                },
                categories = new[] { "games", "entertainment" },
                shortcuts = new[]
                {
                    new
                    {
                        name = "New WordScape Game",
                        short_name = "New Game",
                        description = "Start a new WordScape puzzle game",
                        url = "/wordscape",
                        icons = new[]
                        {
                            new
                            {
                                src = "icon.svg",
                                sizes = "any",
                                type = "image/svg+xml"
                            }
                        }
                    }
                },
                display_override = new[] { "window-controls-overlay", "standalone", "minimal-ui" },
                edge_side_panel = new { },
                prefer_related_applications = false
            };

            return Ok(manifest);
        }
    }
}