namespace TelltaleModLauncher.Core;

/// <summary>
/// Games the launcher knows about. The names double as the ModCompatibility value in a
/// mod's info json, and the numbers are what the config file stores, so neither can change.
/// </summary>
public enum GameVersion
{
    The_Walking_Dead_Definitive_Series = 0,
    The_Walking_Dead_Season_Four = 1,
    The_Walking_Dead_Season_Three = 2,
    The_Walking_Dead_Michonne = 3,
    The_Walking_Dead_Season_Two = 4,
    The_Walking_Dead_Season_One = 5,
    Game_Of_Thrones = 6,
    The_Wolf_Among_Us = 7,
    Marvels_Gaurdians_Of_The_Galaxy = 8,
    Batman_The_Telltale_Series = 9,
    Batman_The_Enemy_Within = 10,
    Tales_from_The_Borderlands = 11,
    Telltale_Texas_Holdem = 12,
    Bone_Out_from_Boneville = 13,
    Bone_The_Great_Cow_Race = 14,
    CSI_3_Dimensions_of_Murder = 15,
    CSI_Deadly_Intent = 16,
    CSI_Fatal_Conspiracy = 17,
    CSI_Hard_Evidence = 18,
    Jurassic_Park_The_Game = 19,
    Law_and_Order_Legacies = 20,
    Minecraft_Story_Mode_Season_One = 21,
    Minecraft_Story_Mode_Season_Two = 22,
    Poker_Night_at_The_Inventory = 23,
    Poker_Night_2 = 24,
    Sam_and_Max_Season_One_101 = 25,
    Sam_and_Max_Season_One_102 = 26,
    Sam_and_Max_Season_One_103 = 27,
    Sam_and_Max_Season_One_104 = 28,
    Sam_and_Max_Season_One_105 = 29,
    Sam_and_Max_Season_One_106 = 30,
    Sam_and_Max_Season_Two_201 = 31,
    Sam_and_Max_Season_Two_202 = 32,
    Sam_and_Max_Season_Two_203 = 33,
    Sam_and_Max_Season_Two_204 = 34,
    Sam_and_Max_Season_Two_205 = 35,
    Sam_and_Max_Season_Three_301 = 36,
    Sam_and_Max_Season_Three_302 = 37,
    Sam_and_Max_Season_Three_303 = 38,
    Sam_and_Max_Season_Three_304 = 39,
    Sam_and_Max_Season_Three_305 = 40,
    Sam_and_Max_Save_The_World_Remastered = 41,
    Hector_Badge_Of_Carnage_101 = 42,
    Hector_Badge_Of_Carnage_102 = 43,
    Hector_Badge_Of_Carnage_103 = 44,
    Puzzle_Agent_101 = 45,
    Puzzle_Agent_102 = 46,
    Back_To_The_Future_101 = 47,
    Back_To_The_Future_102 = 48,
    Back_To_The_Future_103 = 49,
    Back_To_The_Future_104 = 50,
    Back_To_The_Future_105 = 51,
    Strong_Bad_Cool_Game_for_Attractive_People_101 = 52,
    Strong_Bad_Cool_Game_for_Attractive_People_102 = 53,
    Strong_Bad_Cool_Game_for_Attractive_People_103 = 54,
    Strong_Bad_Cool_Game_for_Attractive_People_104 = 55,
    Strong_Bad_Cool_Game_for_Attractive_People_105 = 56,
    Tales_of_Monkey_Island_101 = 57,
    Tales_of_Monkey_Island_102 = 58,
    Tales_of_Monkey_Island_103 = 59,
    Tales_of_Monkey_Island_104 = 60,
    Tales_of_Monkey_Island_105 = 61,
    Wallace_And_Gromits_Grand_Adventures_101 = 62,
    Wallace_And_Gromits_Grand_Adventures_102 = 63,
    Wallace_And_Gromits_Grand_Adventures_103 = 64,
    Wallace_And_Gromits_Grand_Adventures_104 = 65,
    Other = 66
}

public static class GameCatalog
{
    public static IReadOnlyList<GameVersion> All { get; } = Enum.GetValues<GameVersion>();

    /// <summary>Only the Definitive Series has been tested with the launcher.</summary>
    public static bool IsSupported(GameVersion version) => version == GameVersion.The_Walking_Dead_Definitive_Series;

    public static string DisplayName(GameVersion version) => version switch
    {
        GameVersion.The_Walking_Dead_Definitive_Series => "The Walking Dead: The Telltale Definitive Series",
        GameVersion.The_Walking_Dead_Michonne => "The Walking Dead: Michonne",
        GameVersion.Marvels_Gaurdians_Of_The_Galaxy => "Marvel's Guardians of the Galaxy",
        GameVersion.Batman_The_Telltale_Series => "Batman: The Telltale Series",
        GameVersion.Batman_The_Enemy_Within => "Batman: The Enemy Within",
        GameVersion.Telltale_Texas_Holdem => "Telltale Texas Hold'em",
        GameVersion.Jurassic_Park_The_Game => "Jurassic Park: The Game",
        GameVersion.Law_and_Order_Legacies => "Law & Order: Legacies",
        GameVersion.Sam_and_Max_Save_The_World_Remastered => "Sam & Max Save the World Remastered",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_101 => "Wallace & Gromit's Grand Adventures 101",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_102 => "Wallace & Gromit's Grand Adventures 102",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_103 => "Wallace & Gromit's Grand Adventures 103",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_104 => "Wallace & Gromit's Grand Adventures 104",
        _ => version.ToString().Replace('_', ' ')
    };

    /// <summary>
    /// Other names mods use for a game. Every mod listed on the wiki says
    /// "The_Walking_Dead_Definitive_Edition", which was never one of the names above.
    /// </summary>
    private static readonly Dictionary<string, GameVersion> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["The_Walking_Dead_Definitive_Edition"] = GameVersion.The_Walking_Dead_Definitive_Series,
    };

    /// <summary>Parses a mod's ModCompatibility value.</summary>
    public static bool TryParse(string? value, out GameVersion version)
    {
        var text = value?.Trim();
        if (text != null && Aliases.TryGetValue(text, out version))
            return true;
        return Enum.TryParse(text, ignoreCase: true, out version) && Enum.IsDefined(version);
    }

    /// <summary>LibTelltale's ID for each game, kept for resource description editing.</summary>
    public static string LibTelltaleId(GameVersion version) => version switch
    {
        GameVersion.Back_To_The_Future_101 => "bttf101",
        GameVersion.Back_To_The_Future_102 => "bttf102",
        GameVersion.Back_To_The_Future_103 => "bttf103",
        GameVersion.Back_To_The_Future_104 => "bttf104",
        GameVersion.Back_To_The_Future_105 => "bttf105",
        GameVersion.Batman_The_Telltale_Series => "batman",
        GameVersion.Batman_The_Enemy_Within => "batman2",
        GameVersion.Bone_Out_from_Boneville => "boneville",
        GameVersion.Bone_The_Great_Cow_Race => "cowrace",
        GameVersion.CSI_3_Dimensions_of_Murder => "csi3dimensions",
        GameVersion.CSI_Deadly_Intent => "csideadly",
        GameVersion.CSI_Fatal_Conspiracy => "csifatal",
        GameVersion.CSI_Hard_Evidence => "csihard",
        GameVersion.Game_Of_Thrones => "thrones",
        GameVersion.Hector_Badge_Of_Carnage_101 => "hector101",
        GameVersion.Hector_Badge_Of_Carnage_102 => "hector102",
        GameVersion.Hector_Badge_Of_Carnage_103 => "hector103",
        GameVersion.Jurassic_Park_The_Game => "jurrassicpark",
        GameVersion.Law_and_Order_Legacies => "lawandorder",
        GameVersion.Marvels_Gaurdians_Of_The_Galaxy => "marvel",
        GameVersion.Minecraft_Story_Mode_Season_One => "mcsm",
        GameVersion.Minecraft_Story_Mode_Season_Two => "mc2",
        GameVersion.Poker_Night_at_The_Inventory => "celebritypoker",
        GameVersion.Poker_Night_2 => "celebritypoker2",
        GameVersion.Puzzle_Agent_101 => "grickle101",
        GameVersion.Puzzle_Agent_102 => "grickle102",
        GameVersion.Sam_and_Max_Save_The_World_Remastered => "sammaxremaster",
        GameVersion.Sam_and_Max_Season_One_101 => "sammax101",
        GameVersion.Sam_and_Max_Season_One_102 => "sammax102",
        GameVersion.Sam_and_Max_Season_One_103 => "sammax103",
        GameVersion.Sam_and_Max_Season_One_104 => "sammax104",
        GameVersion.Sam_and_Max_Season_One_105 => "sammax105",
        GameVersion.Sam_and_Max_Season_One_106 => "sammax106",
        GameVersion.Sam_and_Max_Season_Three_301 => "sammax301",
        GameVersion.Sam_and_Max_Season_Three_302 => "sammax302",
        GameVersion.Sam_and_Max_Season_Three_303 => "sammax303",
        GameVersion.Sam_and_Max_Season_Three_304 => "sammax304",
        GameVersion.Sam_and_Max_Season_Three_305 => "sammax305",
        GameVersion.Sam_and_Max_Season_Two_201 => "sammax201",
        GameVersion.Sam_and_Max_Season_Two_202 => "sammax202",
        GameVersion.Sam_and_Max_Season_Two_203 => "sammax203",
        GameVersion.Sam_and_Max_Season_Two_204 => "sammax204",
        GameVersion.Sam_and_Max_Season_Two_205 => "sammax205",
        GameVersion.Strong_Bad_Cool_Game_for_Attractive_People_101 => "sbcg4ap101",
        GameVersion.Strong_Bad_Cool_Game_for_Attractive_People_102 => "sbcg4ap102",
        GameVersion.Strong_Bad_Cool_Game_for_Attractive_People_103 => "sbcg4ap103",
        GameVersion.Strong_Bad_Cool_Game_for_Attractive_People_104 => "sbcg4ap104",
        GameVersion.Strong_Bad_Cool_Game_for_Attractive_People_105 => "sbcg4ap105",
        GameVersion.Tales_from_The_Borderlands => "borderlands",
        GameVersion.Tales_of_Monkey_Island_101 => "monkeyisland101",
        GameVersion.Tales_of_Monkey_Island_102 => "monkeyisland102",
        GameVersion.Tales_of_Monkey_Island_103 => "monkeyisland103",
        GameVersion.Tales_of_Monkey_Island_104 => "monkeyisland104",
        GameVersion.Tales_of_Monkey_Island_105 => "monkeyisland105",
        GameVersion.Telltale_Texas_Holdem => "texasholdem",
        GameVersion.The_Walking_Dead_Season_One => "twd1",
        GameVersion.The_Walking_Dead_Definitive_Series => "wdc",
        GameVersion.The_Walking_Dead_Michonne => "michonne",
        GameVersion.The_Walking_Dead_Season_Four => "wd4",
        GameVersion.The_Walking_Dead_Season_Three => "wd3",
        GameVersion.The_Walking_Dead_Season_Two => "wd2",
        GameVersion.The_Wolf_Among_Us => "fables",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_101 => "wag101",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_102 => "wag102",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_103 => "wag103",
        GameVersion.Wallace_And_Gromits_Grand_Adventures_104 => "wag104",
        _ => ""
    };
}
