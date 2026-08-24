namespace Ignyos.LanPortal.Api.Services;

public interface IHostUiStateStore
{
    IReadOnlyDictionary<string, bool> GetPageState(string pageKey);

    void SetSectionState(string pageKey, string sectionKey, bool isExpanded);
}
