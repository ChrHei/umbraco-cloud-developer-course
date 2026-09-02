using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Scoping;

namespace UmbracoProject.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class CloudDeployWebhookController(
        IContentService contentService,
        IConfiguration config,
        ICoreScopeProvider scopeProvider,
        ILogger<CloudDeployWebhookController> logger) : ControllerBase
    {
        [HttpGet("cloud-deploy")]
        [HttpPost("cloud-deploy")]
        public IActionResult ReceiveFromCloudDeploy(
            [FromBody] JsonElement payload,
            [FromQuery(Name = "t")] string? token = null)
        {
            return HandleWebhook(payload, token);
        }

        private IActionResult HandleWebhook(JsonElement payload, string? token)
        {
            var section = config.GetSection("DeploymentWebhook");

            var secret = section["Secret"] ?? string.Empty;
            logger.LogInformation("Webhook called. Token: {Token}, SecretConfigured: {HasSecret}", token, !string.IsNullOrEmpty(secret));

            if (string.IsNullOrWhiteSpace(token) ||
                !string.Equals(token, secret, StringComparison.Ordinal))
            {
                logger.LogWarning("Unauthorized webhook call. Token mismatch.");
                return Unauthorized();
            }

            if (!Guid.TryParse(section["ContentKey"], out var contentKey))
            {
                logger.LogError("Invalid ContentKey in configuration: {Key}", section["ContentKey"]);
                return BadRequest("Invalid ContentKey.");
            }

            var propAlias = section["PropertyAlias"] ?? "deploymentData";
            var content = contentService.GetById(contentKey);

            if (content is null)
            {
                logger.LogError("Content not found for key {Key}", contentKey);
                return NotFound();
            }

            using (var scope = scopeProvider.CreateCoreScope())
            {
                content.SetValue(propAlias, payload.GetRawText());
                contentService.Save(content);

                var cultures = content.ContentType.VariesByCulture()
                    ? (content.AvailableCultures ?? Enumerable.Empty<string>()).ToArray()
                    : Array.Empty<string>();

                var publishResult = contentService.Publish(content, cultures, -1);
                scope.Complete();

                if (!publishResult.Success)
                    return StatusCode(500, "Publish failed.");
            }

            return Ok(new { ok = true });
        }
    }
}