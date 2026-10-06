namespace Pkmds.Rcl.Components.MainTabPages.Trainer;

public partial class TrainerInfoSav5Section
{
    // Matches the PKHeX WinForms SAV_Misc5 numeric limits.
    private const int MaxEntralinkLevel = 999;

    private static readonly PassPower5[] PassPowers = Enum.GetValues<PassPower5>();
    private static readonly KeyType5[] KeyTypes = Enum.GetValues<KeyType5>();

    private EntreeForestArea[] ForestAreas = [];
    private int selectedArea;
    private List<ForestSlotView> areaSlots = [];
    private ForestSlotView? selectedSlot;
    private int forestUnlockedAreas = 2;
    private bool forestNinthArea;
    private SAV5? loadedFor;

    [Parameter]
    [EditorRequired]
    public SAV5 SaveFile { get; set; } = null!;

    /// <summary>Immutable snapshot of an Entrée Forest slot, read while the block was decrypted.</summary>
    private sealed record ForestSlotView(int Index, ushort Species, ushort Move, byte Gender, byte Form);

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(loadedFor, SaveFile))
        {
            return;
        }

        loadedFor = SaveFile;
        selectedSlot = null;
        WithForest(forest =>
        {
            forestUnlockedAreas = forest.Unlock38Areas + 2;
            forestNinthArea = forest.Unlock9thArea;
            ForestAreas = [.. forest.Slots.Select(s => s.Area).Distinct()];
        }, markEdited: false);
        selectedArea = ForestAreas.Length > 0
            ? (int)ForestAreas[0]
            : 0;
        LoadAreaSlots();
    }

    // The Entrée Forest block is XOR-encrypted in the save. PKHeX decrypts it on access and
    // re-encrypts it in SAV5.GetFinalData; we re-encrypt immediately after every read/write
    // so the in-memory save (and any backup taken from it) never holds plaintext forest data.
    private void WithForest(Action<EntreeForest> action, bool markEdited = true)
    {
        var forest = SaveFile.EntreeForest;
        forest.StartAccess();
        try
        {
            action(forest);
        }
        finally
        {
            forest.EndAccess();
        }

        if (markEdited)
        {
            MarkEdited();
        }
    }

    private void MarkEdited() => SaveFile.State.Edited = true;

    private void LoadAreaSlots()
    {
        var area = (EntreeForestArea)selectedArea;
        var slots = new List<ForestSlotView>();
        WithForest(forest =>
        {
            var all = forest.Slots;
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].Area == area)
                {
                    slots.Add(new ForestSlotView(i, all[i].Species, all[i].Move, all[i].Gender, all[i].Form));
                }
            }
        }, markEdited: false);
        areaSlots = slots;
        if (selectedSlot is { } sel)
        {
            selectedSlot = areaSlots.FirstOrDefault(s => s.Index == sel.Index);
        }
    }

    private void SelectArea(int area)
    {
        selectedArea = area;
        selectedSlot = null;
        LoadAreaSlots();
    }

    private void SelectSlot(ForestSlotView slot) => selectedSlot = slot;

    private void UpdateSlot(Action<EntreeSlot> edit)
    {
        if (selectedSlot is not { } sel)
        {
            return;
        }

        WithForest(forest => edit(forest.Slots[sel.Index]));
        LoadAreaSlots();
    }

    private void SetForestUnlockedAreas(int value)
    {
        var clamped = Math.Clamp(value, 2, 8);
        WithForest(forest => forest.Unlock38Areas = clamped - 2);
        forestUnlockedAreas = clamped;
    }

    private void SetForestNinthArea(bool value)
    {
        WithForest(forest => forest.Unlock9thArea = value);
        forestNinthArea = value;
    }

    private void UnlockAllForestAreas()
    {
        WithForest(forest => forest.UnlockAllAreas());
        forestUnlockedAreas = 8;
        forestNinthArea = true;
        Snackbar.Add("All Entrée Forest areas unlocked.", Severity.Success);
    }

    private void SetEntralink(Action<Entralink5> edit)
    {
        edit(SaveFile.Entralink);
        MarkEdited();
    }

    private static int GetPassPower(Entralink5B2W2 pass, int index) => index switch
    {
        0 => pass.PassPower1,
        1 => pass.PassPower2,
        _ => pass.PassPower3,
    };

    private void SetPassPower(Entralink5B2W2 pass, int index, int value)
    {
        var b = (byte)value;
        switch (index)
        {
            case 0:
                pass.PassPower1 = b;
                break;
            case 1:
                pass.PassPower2 = b;
                break;
            default:
                pass.PassPower3 = b;
                break;
        }

        MarkEdited();
    }

    private void UnlockAllFunfestMissions()
    {
        if (SaveFile is not SAV5B2W2 b2w2)
        {
            return;
        }

        b2w2.Festa.UnlockAllFunfestMissions();
        MarkEdited();
        Snackbar.Add("All Funfest Missions unlocked.", Severity.Success);
    }

    private void ObtainAllKeys(SAV5B2W2 b2w2)
    {
        foreach (var key in KeyTypes)
        {
            b2w2.Keys.SetIsKeyObtained(key, true);
            b2w2.Keys.SetIsKeyUnlocked(key, true);
        }

        MarkEdited();
        Snackbar.Add("All Key System keys obtained and unlocked.", Severity.Success);
    }

    private void GiveAllMedals(SAV5B2W2 b2w2)
    {
        b2w2.Medals.GiveAll(EncounterDate.GetDateNDS());
        MarkEdited();
        Snackbar.Add("All medals obtained.", Severity.Success);
    }

    private static string KeyName(KeyType5 key) => key switch
    {
        KeyType5.Easy => "Easy Key (Easy Mode)",
        KeyType5.Challenge => "Challenge Key (Challenge Mode)",
        KeyType5.City => "City Key (Black City / White Forest)",
        KeyType5.Iron => "Iron Key (Iron Chamber)",
        KeyType5.Iceberg => "Iceberg Key (Iceberg Chamber)",
        _ => key.ToString(),
    };

    private static string DescribeArea(EntreeForestArea area)
    {
        if (area.HasFlag(EntreeForestArea.Deepest))
        {
            return "Deepest area";
        }

        var position = area.HasFlag(EntreeForestArea.Left)
            ? "Left"
            : area.HasFlag(EntreeForestArea.Right)
                ? "Right"
                : "Center";
        var depth = (area & ~(EntreeForestArea.Left | EntreeForestArea.Right | EntreeForestArea.Center)).ToString();
        return $"{depth} area — {position}";
    }

    private static string FormatEnumName(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string GetSpeciesName(ushort species) =>
        species < GameInfo.Strings.specieslist.Length
            ? GameInfo.Strings.specieslist[species]
            : $"#{species}";

    private static byte GetDefaultGender(ushort species)
    {
        if (species == 0)
        {
            return 0;
        }

        var pi = PersonalTable.B2W2[species];
        return pi.Genderless
            ? (byte)2
            : pi.OnlyFemale
                ? (byte)1
                : (byte)0;
    }

    private static IEnumerable<(int Value, string Text)> GetGenderChoices(ushort species)
    {
        if (species == 0)
        {
            yield return (0, "-");
            yield break;
        }

        // Mirrors SAV_Misc5.GetGenderChoices.
        var pi = PersonalTable.B2W2[species];
        if (pi.Genderless)
        {
            yield return (2, "Genderless");
            yield break;
        }

        if (!pi.OnlyFemale)
        {
            yield return (0, "Male");
        }

        if (!pi.OnlyMale)
        {
            yield return (1, "Female");
        }
    }

    private Task<IEnumerable<ComboItem>> SearchPokemonNames(string? value, CancellationToken token) =>
        Task.FromResult(AppService.SearchPokemonNames(value ?? string.Empty)
            .Where(s => s.Value <= SaveFile.MaxSpeciesID));

    private Task<IEnumerable<ComboItem>> SearchMoves(string? value, CancellationToken token) =>
        Task.FromResult(AppService.SearchMoves(value ?? string.Empty));
}
