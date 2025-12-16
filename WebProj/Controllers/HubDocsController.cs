using Application.DTOs.Chat;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Reflection;
using WebProj.Hubs;

namespace WebProj.Controllers;

[ApiController]
[Route("api/docs/hubs")]
public class HubDocsController : ControllerBase
{
    /// <summary>
    /// Returns the documentation for the ChatHub, including client-callable methods and server-sent events.
    /// </summary>
    [HttpGet("chat")]
    [ProducesResponseType(typeof(HubDocumentation), StatusCodes.Status200OK)]
    public IActionResult GetChatHubDocs()
    {
        var hubType = typeof(ChatHub);
        
        var methods = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.DeclaringType == hubType)
            .Select(m => new HubMethodDoc
            {
                Name = m.Name,
                Description = m.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description provided.",
                Arguments = m.GetParameters().Select(p => new HubArgumentDoc
                {
                    Name = p.Name ?? "unknown",
                    Type = p.ParameterType.Name
                }).ToList()
            }).ToList();

        var events = new List<HubEventDoc>
        {
            new()
            {
                Name = "ReceiveMessage",
                Description = "Triggered when a new message is sent to the chat group.",
                Arguments = new List<HubArgumentDoc>
                {
                    new() { Name = "message", Type = nameof(MessageDto) }
                }
            }
        };

        return Ok(new HubDocumentation
        {
            HubName = nameof(ChatHub),
            ClientMethods = methods,
            ServerEvents = events
        });
    }
}

public class HubDocumentation
{
    public string HubName { get; set; } = string.Empty;
    public List<HubMethodDoc> ClientMethods { get; set; } = new();
    public List<HubEventDoc> ServerEvents { get; set; } = new();
}

public class HubMethodDoc
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<HubArgumentDoc> Arguments { get; set; } = new();
}

public class HubEventDoc
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<HubArgumentDoc> Arguments { get; set; } = new();
}

public class HubArgumentDoc
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
