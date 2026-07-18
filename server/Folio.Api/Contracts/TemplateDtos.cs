using System.ComponentModel.DataAnnotations;

namespace Folio.Api.Contracts;

/// <summary>A page template in the workspace gallery.</summary>
public record TemplateResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    string SourceTitle,
    string? SourceIcon,
    int BlockCount,
    string? CreatedByName,
    DateTime CreatedAt);

// Required fields are nullable so a missing value fails validation (400).

/// <summary>Create a template from an existing page.</summary>
public record CreateTemplateRequest(
    [Required][MaxLength(200)] string? Name,
    [MaxLength(1000)] string? Description);

/// <summary>Instantiate a template as a new page (optionally nested under a parent).</summary>
public record InstantiateTemplateRequest(Guid? ParentId);

/// <summary>A page's Markdown export.</summary>
public record ExportResponse(string Filename, string Markdown);
