using PortfolioLens.Core.Facts;

namespace PortfolioLens.Extraction;

// Project is null when the file cannot be read (broken XML). Errors can have items in both cases.
internal sealed record ProjectReadResult(ProjectFacts? Project, IReadOnlyList<ScanError> Errors);