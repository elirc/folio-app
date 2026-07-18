using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(NotificationService notifications) : ControllerBase
{
    /// <summary>The current member's notifications, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> List(CancellationToken ct) =>
        Ok(await notifications.GetForMemberAsync(ct));

    /// <summary>Count of unread notifications.</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken ct) =>
        Ok(new UnreadCountResponse(await notifications.UnreadCountAsync(ct)));

    /// <summary>Mark one notification read.</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) =>
        await notifications.MarkReadAsync(id, ct)
            ? NoContent()
            : Problem(statusCode: 404, detail: "Notification not found.");

    /// <summary>Mark all of the member's notifications read.</summary>
    [HttpPost("read-all")]
    public async Task<ActionResult<UnreadCountResponse>> ReadAll(CancellationToken ct) =>
        Ok(new UnreadCountResponse(await notifications.MarkAllReadAsync(ct)));
}
