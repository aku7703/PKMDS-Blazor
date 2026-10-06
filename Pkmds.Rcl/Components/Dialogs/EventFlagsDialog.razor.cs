namespace Pkmds.Rcl.Components.Dialogs;

public partial class EventFlagsDialog
{
    private const int MaxRows = 150;

    private static readonly string[] QuickSearches = ["Ticket", "Travel", "Letter", "Card", "Flute", "Map", "Pass", "Event"];

    private EventWorkspace<IEventFlag37, ushort>? workspace;
    private string loadError = string.Empty;
    private string? search;
    private int customFlag;

    [Parameter]
    [EditorRequired]
    public SaveFile SaveFile { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IEventFlag37 Source { get; set; } = null!;

    [Parameter]
    public GameVersion Version { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override void OnInitialized()
    {
        try
        {
            workspace = new EventWorkspace<IEventFlag37, ushort>(Source, Version);
        }
        catch (ArgumentOutOfRangeException)
        {
            loadError = $"PKHeX has no event flag data for {Version}.";
        }
    }

    private IEnumerable<NamedEventValue> FilteredFlags =>
        workspace is null
            ? []
            : workspace.Labels.Flag.Where(f => (uint)f.Index < (uint)workspace.Flags.Length && Matches(f.Name));

    private IEnumerable<NamedEventWork> FilteredWork =>
        workspace is null
            ? []
            : workspace.Labels.Work.Where(w => (uint)w.Index < (uint)workspace.Values.Length && Matches(w.Name));

    private bool Matches(string name) =>
        string.IsNullOrWhiteSpace(search) || name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private bool GetCustomFlag() =>
        workspace is not null && (uint)customFlag < (uint)workspace.Flags.Length && workspace.Flags[customFlag];

    private void SetCustomFlag(bool value)
    {
        if (workspace is not null && (uint)customFlag < (uint)workspace.Flags.Length)
        {
            workspace.Flags[customFlag] = value;
        }
    }

    private void SaveChanges()
    {
        if (workspace is null)
        {
            return;
        }

        // EventWorkspace.Save writes flags + work back and refreshes the SM/USUM QR constants.
        workspace.Save();
        SaveFile.State.Edited = true;
        Snackbar.Add("Event flags saved.", Severity.Success);
        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
