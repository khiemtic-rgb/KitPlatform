using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Application.CustomerApp;

/// <summary>
/// Thẻ chăm sóc theo nhóm thuốc mãn tính + tuổi. Luật cứng, không LLM, không chẩn đoán.
/// Khí hậu Bắc Bộ (Thái Nguyên) chỉ là lớp phủ — không có nhóm thuốc thì không ra thẻ.
/// </summary>
public static class CustomerCareCardRules
{
    public const int ElderlyAgeYears = 60;
    public const int LookbackDays = 180;
    public const string Disclaimer =
        "Tham khảo theo đơn đã mua tại nhà thuốc. Không chẩn đoán bệnh, không thay lời bác sĩ/dược sĩ. Không tự đổi liều.";
    public const string CtaLabel = "Hỏi dược sĩ";
    public const string CtaPath = "/chat";

    public readonly record struct ClimateFlags(bool Cold, bool Hot, bool Humid);

    public static ClimateFlags ClimateFor(DateOnly today) => today.Month switch
    {
        11 or 12 or 1 => new ClimateFlags(Cold: true, Hot: false, Humid: false),
        2 or 3 => new ClimateFlags(Cold: true, Hot: false, Humid: true),
        4 => new ClimateFlags(Cold: false, Hot: false, Humid: true),
        5 or 6 or 7 or 8 => new ClimateFlags(Cold: false, Hot: true, Humid: false),
        _ => default,
    };

    public static int? AgeYears(DateOnly? birth, DateOnly today)
    {
        if (birth is null || birth.Value > today) return null;
        var age = today.Year - birth.Value.Year;
        if (today < birth.Value.AddYears(age)) age--;
        return age < 0 ? null : age;
    }

    public static CustomerCareCardDto? Pick(CustomerCareCardFacts facts)
    {
        var matches = DetectGroups(facts.Products);
        if (matches.Count == 0) return null;

        var age = AgeYears(facts.DateOfBirth, facts.Today);
        var elderly = age >= ElderlyAgeYears;
        var climate = ClimateFor(facts.Today);
        var picked = ChooseGroup(matches, climate, facts.Today);
        return Build(picked, elderly, climate);
    }

    public static IReadOnlyList<(string Group, string ProductName)> DetectGroups(
        IReadOnlyList<CustomerCareCardProductRow> products)
    {
        var found = new List<(string Group, string ProductName)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var product in products)
        {
            var hay = Fold(
                $"{product.ProductName} {product.GenericName} {product.CategoryName} {product.Ingredients}");
            if (hay.Length == 0) continue;

            foreach (var (group, phrases) in GroupPhrases)
            {
                if (!phrases.Any(p => ContainsPhrase(hay, p))) continue;
                if (!seen.Add(group)) continue;
                var name = string.IsNullOrWhiteSpace(product.ProductName)
                    ? null
                    : product.ProductName.Trim();
                found.Add((group, name ?? group));
            }
        }

        return found;
    }

    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var norm = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(norm.Length);
        foreach (var c in norm)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c is 'đ' or 'Ð' or 'Đ')
            {
                sb.Append('d');
                continue;
            }

            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    internal static bool ContainsPhrase(string haystack, string phrase)
    {
        if (haystack.Length == 0 || phrase.Length == 0) return false;
        var padded = $" {haystack} ";
        var needle = $" {phrase} ";
        if (padded.Contains(needle, StringComparison.Ordinal)) return true;
        return phrase.Length >= 8 && haystack.Contains(phrase, StringComparison.Ordinal);
    }

    private static (string Group, string ProductName) ChooseGroup(
        IReadOnlyList<(string Group, string ProductName)> matches,
        ClimateFlags climate,
        DateOnly today)
    {
        var ranked = matches
            .Select((m, index) => (
                Match: m,
                Fit: ClimateFits(m.Group, climate) ? 1 : 0,
                Priority: GroupPriority(m.Group),
                Index: index))
            .OrderByDescending(x => x.Fit)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Index)
            .ToList();

        var topFit = ranked[0].Fit;
        var top = ranked.Where(x => x.Fit == topFit).ToList();
        if (top.Count == 1) return top[0].Match;
        return top[today.DayOfYear % top.Count].Match;
    }

    private static bool ClimateFits(string group, ClimateFlags climate) => group switch
    {
        CustomerCareCardGroups.Respiratory => climate.Cold,
        CustomerCareCardGroups.Hypertension => climate.Cold || climate.Hot,
        CustomerCareCardGroups.Diabetes => climate.Hot,
        CustomerCareCardGroups.Joint => climate.Cold || climate.Humid,
        _ => false,
    };

    private static int GroupPriority(string group) => group switch
    {
        CustomerCareCardGroups.Respiratory => 0,
        CustomerCareCardGroups.Hypertension => 1,
        CustomerCareCardGroups.Diabetes => 2,
        CustomerCareCardGroups.Joint => 3,
        _ => 9,
    };

    private static string? PrimaryClimate(string group, ClimateFlags climate)
    {
        if (group == CustomerCareCardGroups.Respiratory && climate.Cold)
            return CustomerCareCardClimate.Cold;
        if (group == CustomerCareCardGroups.Hypertension && climate.Cold)
            return CustomerCareCardClimate.Cold;
        if (group == CustomerCareCardGroups.Hypertension && climate.Hot)
            return CustomerCareCardClimate.Hot;
        if (group == CustomerCareCardGroups.Diabetes && climate.Hot)
            return CustomerCareCardClimate.Hot;
        if (group == CustomerCareCardGroups.Joint && climate.Humid)
            return CustomerCareCardClimate.Humid;
        if (group == CustomerCareCardGroups.Joint && climate.Cold)
            return CustomerCareCardClimate.Cold;
        return null;
    }

    private static CustomerCareCardDto Build(
        (string Group, string ProductName) match,
        bool elderly,
        ClimateFlags climate)
    {
        var overlay = PrimaryClimate(match.Group, climate);
        var (title, body) = Copy(match.Group, overlay, elderly);
        var code = string.Join('_', new[]
        {
            match.Group,
            elderly ? "elderly" : null,
            overlay,
        }.Where(s => !string.IsNullOrEmpty(s)));

        var hint = string.IsNullOrWhiteSpace(match.ProductName)
            ? null
            : $"Theo đơn gần đây: {match.ProductName.Trim()}";

        return new CustomerCareCardDto(
            code,
            match.Group,
            title,
            body,
            Disclaimer,
            CtaLabel,
            CtaPath,
            overlay,
            elderly,
            hint);
    }

    private static (string Title, string Body) Copy(string group, string? climate, bool elderly)
    {
        var extra = elderly
            ? group switch
            {
                CustomerCareCardGroups.Hypertension =>
                    " Người lớn tuổi nên đo huyết áp khi chóng mặt hoặc mệt lạ, rồi hỏi dược sĩ.",
                CustomerCareCardGroups.Joint =>
                    " Nếu sưng nhiều hoặc đau tăng, chat dược sĩ trước khi đổi thuốc.",
                CustomerCareCardGroups.Respiratory =>
                    " Khó thở tăng hoặc sốt — hỏi dược sĩ, không tự tăng liều xịt.",
                CustomerCareCardGroups.Diabetes =>
                    " Chóng mặt, vã mồ hôi nhiều — hỏi dược sĩ, đừng tự ngưng thuốc.",
                _ => string.Empty,
            }
            : string.Empty;

        return group switch
        {
            CustomerCareCardGroups.Hypertension => climate switch
            {
                CustomerCareCardClimate.Cold => (
                    "Trời lạnh — giữ đều thuốc huyết áp",
                    "Bạn đang dùng thuốc nhóm huyết áp. Uống đúng giờ đã kê, không tự tăng/giảm hoặc ngưng liều. Trời lạnh mạch dễ co — giữ ấm, đứng dậy từ từ." + extra),
                CustomerCareCardClimate.Hot => (
                    "Trời nóng — đừng bỏ liều huyết áp",
                    "Bạn đang dùng thuốc nhóm huyết áp. Uống đúng giờ đã kê, không tự tăng/giảm hoặc ngưng liều. Trời nóng dễ mất nước — uống đủ nước, đứng dậy từ từ." + extra),
                _ => (
                    "Giữ đều thuốc huyết áp hôm nay",
                    "Bạn đang dùng thuốc nhóm huyết áp. Uống đúng giờ đã kê, không tự tăng/giảm hoặc ngưng liều." + extra),
            },
            CustomerCareCardGroups.Joint => climate switch
            {
                CustomerCareCardClimate.Humid => (
                    "Nồm ẩm — khớp dễ ê hơn",
                    "Bạn đang dùng thuốc nhóm xương khớp. Dùng đúng hướng dẫn trên đơn. Nồm ẩm khớp dễ ê — giữ ấm, đừng tự thêm thuốc giảm đau." + extra),
                CustomerCareCardClimate.Cold => (
                    "Trời lạnh — giữ đều thuốc khớp",
                    "Bạn đang dùng thuốc nhóm xương khớp. Dùng đúng hướng dẫn trên đơn. Lạnh khớp dễ ê hơn — giữ ấm, đừng tự thêm thuốc giảm đau." + extra),
                _ => (
                    "Giữ đều thuốc xương khớp hôm nay",
                    "Bạn đang dùng thuốc nhóm xương khớp. Dùng đúng hướng dẫn trên đơn, đừng tự thêm thuốc giảm đau." + extra),
            },
            CustomerCareCardGroups.Respiratory => climate switch
            {
                CustomerCareCardClimate.Cold => (
                    "Trời lạnh — giữ đều thuốc hô hấp",
                    "Bạn đang dùng thuốc nhóm hô hấp. Dùng đúng thuốc đang có, không tự thêm kháng sinh. Lạnh khô dễ kích thích đường thở — giữ ấm cổ, tránh gió lùa." + extra),
                _ => (
                    "Giữ đều thuốc hô hấp hôm nay",
                    "Bạn đang dùng thuốc nhóm hô hấp. Dùng đúng thuốc đang có, không tự thêm kháng sinh." + extra),
            },
            CustomerCareCardGroups.Diabetes => climate switch
            {
                CustomerCareCardClimate.Hot => (
                    "Trời nóng — giữ nước khi dùng thuốc đường huyết",
                    "Bạn đang dùng thuốc nhóm đường huyết. Ăn uống đều, không tự đổi liều. Trời nóng dễ mất nước — uống đủ nước, nhận biết mệt hoặc khát bất thường." + extra),
                _ => (
                    "Giữ đều thuốc đường huyết hôm nay",
                    "Bạn đang dùng thuốc nhóm đường huyết. Ăn uống đều, không tự đổi liều." + extra),
            },
            _ => ("Lời nhắc theo đơn", "Dùng đúng thuốc trên đơn. Có thắc mắc thì hỏi dược sĩ." + extra),
        };
    }

    private static readonly (string Group, string[] Phrases)[] GroupPhrases =
    [
        (CustomerCareCardGroups.Hypertension, [
            "huyet ap", "tang huyet ap",
            "amlodipin", "amlodipine", "norvasc",
            "losartan", "telmisartan", "valsartan", "irbesartan", "candesartan", "olmesartan",
            "enalapril", "perindopril", "ramipril", "lisinopril", "coversyl", "prestarium",
            "bisoprolol", "metoprolol", "carvedilol", "atenolol", "nebivolol",
            "nifedipin", "nifedipine", "felodipin", "felodipine",
            "hydrochlorothiazide", "hydroclorothiazid", "indapamid", "indapamide",
            "spironolacton", "spironolactone",
        ]),
        (CustomerCareCardGroups.Joint, [
            "xuong khop", "viem khop", "thuoc khop",
            "glucosamin", "glucosamine", "chondroitin",
            "celecoxib", "celebrex", "meloxicam", "mobic",
            "allopurinol", "colchicin", "colchicine", "febuxostat",
            "methotrexate", "gout",
        ]),
        (CustomerCareCardGroups.Respiratory, [
            "hen suyen", "hen phe quan", "thuoc hen", "benh hen",
            "copd", "benh phoi tac nghe",
            "salbutamol", "albuterol", "ventolin",
            "budesonid", "budesonide", "seretide", "symbicort", "pulmicort",
            "montelukast", "singulair",
            "formoterol", "salmeterol", "tiotropium", "spiriva",
            "acetylcystein", "acetylcysteine", "fluimucil",
            "ambroxol",
        ]),
        (CustomerCareCardGroups.Diabetes, [
            "tieu duong", "duong huyet",
            "metformin", "glucophage",
            "gliclazid", "gliclazide", "diamicron",
            "glimepirid", "glimepiride",
            "sitagliptin", "dapagliflozin", "empagliflozin",
            "insulin",
        ]),
    ];
}
