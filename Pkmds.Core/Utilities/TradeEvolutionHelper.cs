namespace Pkmds.Core.Utilities;

/// <summary>
/// Simulates the evolution step of a real in-game trade: after a Pokémon arrives in the
/// destination save, determine whether it would evolve there (Trade, TradeHeldItem, or
/// the Shelmet/Karrablast exchange) and apply the species change.
/// </summary>
/// <remarks>
/// Held-item IDs are compared in "display" (Gen 4+) item space via
/// <see cref="ItemConverter.GetItemDisplay"/> so Gen 2/3 internal item IDs line up with
/// the evolution table arguments regardless of which save the item came from.
/// </remarks>
public static class TradeEvolutionHelper
{
    /// <summary>Everstone, in Gen 4+ item ID space. Holding it blocks trade evolution.</summary>
    public const int EverstoneItemId = 229;

    /// <summary>
    /// Result of a trade evolution lookup.
    /// </summary>
    /// <param name="Method">Evolution branch from PKHeX's <see cref="EvolutionTree"/>.</param>
    /// <param name="ConsumesHeldItem">True when the evolution uses up the held item (TradeHeldItem).</param>
    public readonly record struct TradeEvolution(EvolutionMethod Method, bool ConsumesHeldItem);

    /// <summary>
    /// Returns true if the destination save is a mainline game where receiving a Pokémon via
    /// trade triggers evolution. Colosseum/XD, Battle Revolution, Stadium, Let's Go and Legends
    /// titles don't perform trade evolutions.
    /// </summary>
    public static bool SupportsTradeEvolution(SaveFile destSave) =>
        destSave is SAV1 or SAV2 or SAV3 or SAV4 or SAV5 or SAV6 or SAV7 or SAV8SWSH or SAV8BS or SAV9SV;

    /// <summary>
    /// Finds the trade evolution (if any) that <paramref name="pk"/> would undergo when received
    /// by <paramref name="destSave"/>.
    /// </summary>
    /// <param name="pk">Entity already converted to the destination's format.</param>
    /// <param name="destSave">Receiving save file.</param>
    /// <param name="heldItem">The item the Pokémon was holding when traded, in its source format's item space.</param>
    /// <param name="heldItemContext">Context the <paramref name="heldItem"/> ID belongs to.</param>
    /// <param name="partnerSpecies">Species it was exchanged for (0 for a one-way transfer).</param>
    /// <param name="evolution">The matching evolution.</param>
    public static bool TryGetTradeEvolution(PKM pk, SaveFile destSave, int heldItem, EntityContext heldItemContext,
        ushort partnerSpecies, out TradeEvolution evolution)
    {
        evolution = default;
        if (pk.Species == 0 || pk.IsEgg || !SupportsTradeEvolution(destSave))
        {
            return false;
        }

        var itemDisplay = heldItem <= 0
            ? 0
            : ItemConverter.GetItemDisplay(heldItem, heldItemContext);
        if (itemDisplay == EverstoneItemId)
        {
            return false;
        }

        var context = pk.Context;
        var tree = EvolutionTree.GetEvolutionTree(context);
        foreach (var method in tree.Forward.GetForward(pk.Species, pk.Form).Span)
        {
            if (!method.Method.IsTrade || method.Species == 0 || method.Species > destSave.MaxSpeciesID)
            {
                continue;
            }

            // FRLG cannot evolve into non-Kanto species until the National Dex is obtained.
            if (destSave is SAV3FRLG { NationalDex: false } && method.Species > (ushort)Species.Mew)
            {
                continue;
            }

            var requiredItem = GetRequiredItemDisplay(pk, method, context);
            switch (method.Method)
            {
                case EvolutionType.TradeShelmetKarrablast:
                    if (!IsShelmetKarrablastPair(pk.Species, partnerSpecies))
                    {
                        continue;
                    }

                    evolution = new TradeEvolution(method, false);
                    return true;
                default:
                    if (requiredItem == 0)
                    {
                        evolution = new TradeEvolution(method, false);
                        return true;
                    }

                    if (requiredItem == itemDisplay)
                    {
                        evolution = new TradeEvolution(method, true);
                        return true;
                    }

                    continue;
            }
        }

        return false;
    }

    private static bool IsShelmetKarrablastPair(ushort species, ushort partner) =>
        (species, partner) is ((ushort)Species.Shelmet, (ushort)Species.Karrablast)
        or ((ushort)Species.Karrablast, (ushort)Species.Shelmet);

    // Returns the required held item in Gen 4+ ID space, or 0 if no item is needed.
    private static int GetRequiredItemDisplay(PKM pk, EvolutionMethod method, EntityContext context)
    {
        if (method.Method == EvolutionType.TradeHeldItem)
        {
            return ItemConverter.GetItemDisplay(method.Argument, context);
        }

        // PKHeX's Gen 2 evolution table stores every trade evolution as a plain Trade with no
        // item argument. In GSC, Politoed/Slowking/Steelix/Kingdra/Scizor/Porygon2 still need
        // King's Rock / Metal Coat / Dragon Scale / Up-Grade, so borrow the item from Gen 4's
        // table (same species branch; item IDs are already Gen 4+).
        if (context == EntityContext.Gen2 && method.Method == EvolutionType.Trade)
        {
            var tree4 = EvolutionTree.GetEvolutionTree(EntityContext.Gen4);
            foreach (var m4 in tree4.Forward.GetForward(pk.Species, 0).Span)
            {
                if (m4.Species == method.Species && m4.Method == EvolutionType.TradeHeldItem)
                {
                    return m4.Argument;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Applies the evolution to <paramref name="pk"/>: species, form, gender sanity, ability
    /// slot, default nickname and party stats. Trade evolutions have no level requirement, so
    /// the level is left untouched.
    /// </summary>
    public static void ApplyEvolution(PKM pk, EvolutionMethod method)
    {
        var destForm = method.GetDestinationForm(pk.Form);

        // Capture before changing species: Gen 3 computes IsNicknamed from Nickname vs. species name.
        var wasNicknamed = pk.IsNicknamed;
        var abilityIndex = pk.AbilityNumber switch
        {
            2 => 1,
            4 => 2,
            _ => 0,
        };

        pk.Species = method.Species;
        pk.Form = destForm;
        pk.Gender = pk.GetSaneGender();

        if (pk.Format >= 3)
        {
            pk.RefreshAbility(abilityIndex);
        }

        if (!wasNicknamed)
        {
            pk.ClearNickname();
        }

        if (pk.PartyStatsPresent)
        {
            var previousHp = pk.Stat_HPCurrent;
            Span<ushort> stats = stackalloc ushort[6];
            pk.LoadStats(pk.PersonalInfo, stats);
            pk.SetStats(stats);
            pk.Stat_HPCurrent = Math.Min(previousHp, pk.Stat_HPMax);
        }

        pk.RefreshChecksum();
    }
}
