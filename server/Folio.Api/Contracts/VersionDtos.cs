using System.Text.Json;
using Folio.Domain.Enums;

namespace Folio.Api.Contracts;

/// <summary>A single block captured inside a page-version snapshot.</summary>
public record BlockSnapshotDto(
    Guid Id,
    Guid? ParentBlockId,
    BlockType Type,
    int Position,
    JsonElement Content);

/// <summary>How a version differs from the page's current state.</summary>
public record DiffSummary(int Added, int Removed, int Changed);

/// <summary>A row in the page's history list.</summary>
public record VersionSummaryResponse(
    int VersionNumber,
    string Title,
    string? Icon,
    int BlockCount,
    string? CreatedByName,
    string? Label,
    DateTime CreatedAt);

/// <summary>A full version snapshot plus its diff against the current page.</summary>
public record VersionDetailResponse(
    int VersionNumber,
    string Title,
    string? Icon,
    IReadOnlyList<BlockSnapshotDto> Blocks,
    DiffSummary Diff,
    string? CreatedByName,
    string? Label,
    DateTime CreatedAt);
