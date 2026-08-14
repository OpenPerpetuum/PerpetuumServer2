using Perpetuum.AdminTool.Export;

namespace Perpetuum.AdminTool.Avalonia.Tests.ViewModels;

internal sealed class StubContentExporter : IContentExporter
{
    public Task<string> ExportItemAsync(int definition, CancellationToken cancellationToken = default) =>
        Task.FromResult($"item export {definition}");

    public Task<string> ExportRobotAsync(int templateId, CancellationToken cancellationToken = default) =>
        Task.FromResult($"robot export {templateId}");

    public Task<string> ExportSeasonAsync(int seasonId, CancellationToken cancellationToken = default) =>
        Task.FromResult($"season export {seasonId}");
}
