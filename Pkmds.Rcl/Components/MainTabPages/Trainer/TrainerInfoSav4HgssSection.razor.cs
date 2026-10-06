namespace Pkmds.Rcl.Components.MainTabPages.Trainer;

public partial class TrainerInfoSav4HgssSection
{
    // Matches the PKHeX WinForms SAV_Misc4 numeric limits.
    private const uint MaxWalkerValue = 9_999_999;

    private readonly bool[] courses = new bool[SAV4HGSS.PokewalkerCourseFlagCount];

    [Parameter]
    [EditorRequired]
    public SAV4HGSS SaveFile { get; set; } = null!;

    private IReadOnlyList<string> CourseNames
    {
        get
        {
            var names = GameInfo.Strings.walkercourses;
            return names.Length > courses.Length
                ? names[..courses.Length]
                : names;
        }
    }

    protected override void OnParametersSet() => SaveFile.GetPokewalkerCoursesUnlocked(courses);

    private void MarkEdited() => SaveFile.State.Edited = true;

    private void SetCourse(int index, bool value)
    {
        courses[index] = value;
        SaveFile.SetPokewalkerCoursesUnlocked(courses);
        MarkEdited();
    }

    private void UnlockAllCourses()
    {
        SaveFile.PokewalkerCoursesUnlockAll();
        SaveFile.GetPokewalkerCoursesUnlocked(courses);
        MarkEdited();
        Snackbar.Add("All Pokéwalker courses unlocked.", Severity.Success);
    }
}
