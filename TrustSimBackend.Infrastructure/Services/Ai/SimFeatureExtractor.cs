using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Dịch vụ bóc tách đặc trưng số học, khảo sát đa nhịp số (format_rhythms) từ DbContext và tối ưu hóa tổ hợp (sim_categories) - Tầng 1
/// </summary>
public class SimFeatureExtractor : ISimFeatureExtractor
{
    private readonly TrustSimDbContext _context;

    public SimFeatureExtractor(TrustSimDbContext context)
    {
        _context = context;
    }

    private record CadenceTemplate(string RhythmValue, string RhythmCode, string RhythmName, int[] Partitions, float BaseFactor);

    private static readonly List<CadenceTemplate> FallbackCadenceTemplates = new()
    {
        new("2.2.2.2.2", "2_2_2_2_2", "Thế SIM Nhịp Đôi Đại Taxi (2.2.2.2.2)", new[] { 2, 2, 2, 2, 2 }, 1.30f),
        new("2.4.4",     "2_4_4",     "Thế SIM Đại Taxi Tứ Quý Kép (2.4.4)",     new[] { 2, 4, 4 },       1.25f),
        new("2.8",       "2_8",       "Thế SIM Bát Quý Độc Tôn (2.8)",           new[] { 2, 8 },          1.35f),
        new("4.6",       "4_6",       "Thế SIM Lục Quý / Sảnh Rồng (4.6)",       new[] { 4, 6 },          1.25f),
        new("2.2.2.4",   "2_2_2_4",   "Thế SIM Nhịp Chẵn Đuôi Tứ Quý (2.2.2.4)", new[] { 2, 2, 2, 4 },    1.20f),
        new("4.3.3",     "4_3_3",     "Thế SIM Tam Hoa Kép Thân Đuôi (4.3.3)",   new[] { 4, 3, 3 },       1.20f),
        new("3.4.3",     "3_4_3",     "Thế SIM Taxi 3 Cặp (3.4.3)",             new[] { 3, 4, 3 },       1.15f),
        new("3.2.2.3",   "3_2_2_3",   "Thế SIM Gánh Đảo Kép Đối Xứng (3.2.2.3)", new[] { 3, 2, 2, 3 },    1.10f),
        new("5.5",       "5_5",       "Thế SIM Cân Xứng Bán Đoạn (5.5)",         new[] { 5, 5 },          1.15f),
        new("3.3.4",     "3_3_4",     "Thế SIM Chuẩn Viễn Thông (3.3.4)",        new[] { 3, 3, 4 },       1.00f)
    };

    private IReadOnlyList<CadenceTemplate> LoadCadenceTemplatesFromDb()
    {
        try
        {
            var dbRhythms = _context.FormatRhythms
                .AsNoTracking()
                .Where(r => r.IsActive)
                .ToList();

            if (dbRhythms != null && dbRhythms.Count > 0)
            {
                var templates = new List<CadenceTemplate>();
                foreach (var r in dbRhythms)
                {
                    var partitions = ParsePartitionString(r.Value);
                    if (partitions != null && partitions.Length > 0)
                    {
                        var code = string.IsNullOrWhiteSpace(r.Value) ? "SIM_THUONG" : r.Value.Replace(".", "_");
                        templates.Add(new CadenceTemplate(r.Value, code, r.FormatRhythmName, partitions, r.BasePriceFactor));
                    }
                }

                if (templates.Count > 0)
                {
                    return templates;
                }
            }
        }
        catch
        {
            // Dự phòng Fallback khi chưa kết nối được DB
        }

        return FallbackCadenceTemplates;
    }

    private static int[]? ParsePartitionString(string? rhythmValue)
    {
        if (string.IsNullOrWhiteSpace(rhythmValue) || rhythmValue.Equals("NORMAL", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { 3, 3, 4 };
        }

        var parts = rhythmValue.Split(new[] { '.', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var partitions = new List<int>();
        int total = 0;
        foreach (var part in parts)
        {
            if (int.TryParse(part, out int val) && val > 0)
            {
                partitions.Add(val);
                total += val;
            }
            else
            {
                return null;
            }
        }

        return total == 10 ? partitions.ToArray() : null;
    }

    public SimExtractedFeatures ExtractFeatures(string phoneNumber, string? carrierName = null, float prefixScore = 1.0f)
    {
        var raw = phoneNumber ?? string.Empty;
        var clean = Regex.Replace(raw, @"[^\d]", "");

        if (!clean.StartsWith("0") && clean.Length == 9)
        {
            clean = "0" + clean;
        }

        var result = new SimExtractedFeatures
        {
            RawPhoneNumber = raw,
            CleanPhoneNumber = clean,
            CarrierName = carrierName ?? DetectCarrierFromPrefix(clean),
            PrefixReputationScore = prefixScore
        };

        if (clean.Length >= 3)
        {
            result.Prefix = clean.Substring(0, 3);
            if (prefixScore <= 1.0f)
            {
                result.PrefixReputationScore = CalculatePrefixReputationScore(result.Prefix);
            }
        }

        if (clean.Length == 10)
        {
            result.PrefixGroup = $"{clean.Substring(0, 3)} ({result.CarrierName})";
            result.MiddleGroup = clean.Substring(3, 3);
            result.TailGroup = clean.Substring(6, 4);

            int sumDigits = clean.Sum(c => c - '0');
            int pts = sumDigits % 10;
            result.TotalPoints = pts == 0 ? 10 : pts;
        }

        // Khảo sát đa nhịp ngắt và chọn nhịp tối ưu bộc lộ tổ hợp giá trị cực đại
        EvaluateMultiRhythmCadence(clean, result);

        // Phân tích yếu tố kiêng kỵ & phong thủy tổng quan
        ExtractFengShuiModifiers(clean, result);

        return result;
    }

    private void EvaluateMultiRhythmCadence(string clean, SimExtractedFeatures result)
    {
        if (clean.Length != 10)
        {
            result.FormattedNumber = clean;
            result.SelectedRhythmValue = "3.3.4";
            result.DetectedRhythmCode = "SIM_THUONG";
            result.DetectedRhythmName = "SIM Thường";
            result.RhythmBaseFactor = 1.0f;
            ExtractCategoriesForString(clean, result.DetectedCategories);
            return;
        }

        var templates = LoadCadenceTemplatesFromDb();
        CadenceEvaluationResult? bestEvaluation = null;
        CadenceEvaluationResult? default334Evaluation = null;

        foreach (var template in templates)
        {
            var eval = EvaluateCadenceTemplate(clean, template);
            if (template.RhythmValue == "3.3.4")
            {
                default334Evaluation = eval;
            }

            if (bestEvaluation == null || eval.TotalScore > bestEvaluation.TotalScore)
            {
                bestEvaluation = eval;
            }
        }

        var chosen = bestEvaluation ?? default334Evaluation ?? EvaluateCadenceTemplate(clean, templates.LastOrDefault() ?? FallbackCadenceTemplates.Last());

        result.FormattedNumber = chosen.FormattedNumber;
        result.SelectedRhythmValue = chosen.Template.RhythmValue;
        result.DetectedRhythmCode = chosen.Template.RhythmCode;
        result.DetectedRhythmName = chosen.Template.RhythmName;
        result.RhythmBaseFactor = chosen.Template.BaseFactor;
        result.DetectedCategories = chosen.Categories;

        // Sinh giải trình so sánh thế số giữa nhịp tối ưu và nhịp chuẩn 3.3.4
        if (chosen.Template.RhythmValue != "3.3.4" && default334Evaluation != null)
        {
            var sb = new StringBuilder();
            sb.Append($"Khi định dạng theo nhịp **{chosen.FormattedNumber}** ({chosen.Template.RhythmName}), số SIM bộc lộ thế số vượt trội ");
            sb.Append($"với các tổ hợp tài lộc: {string.Join(", ", chosen.Categories.Select(c => c.CategoryName))}. ");
            sb.Append($"So với nhịp chuẩn mặc định `{default334Evaluation.FormattedNumber}` (3.3.4) làm phân đoạn số bị đứt quãng, nhịp tối ưu nâng cao cảm giác phát âm dồn dập, uy lực và gia tăng giá trị thương hiệu lên mức tối đa.");
            result.AlternativeRhythmsComparison = sb.ToString();
        }
        else
        {
            result.AlternativeRhythmsComparison = $"Số SIM đạt sự cân bằng tối ưu theo nhịp chuẩn `{chosen.FormattedNumber}` (3.3.4), các cụm số dễ đọc, dễ nhớ và thanh khoản tốt.";
        }
    }

    private class CadenceEvaluationResult
    {
        public CadenceTemplate Template { get; set; } = null!;
        public string FormattedNumber { get; set; } = string.Empty;
        public List<SimCategoryFeature> Categories { get; set; } = new();
        public double TotalScore { get; set; }
    }

    private CadenceEvaluationResult EvaluateCadenceTemplate(string clean, CadenceTemplate template)
    {
        var formatted = FormatWithPartitions(clean, template.Partitions);
        var categories = new List<SimCategoryFeature>();

        // 1. Kiểm tra Đại Taxi 4 cặp lặp liên tiếp (VD: 09.86.86.86.86 hoặc 09.68.68.68.68)
        if (template.RhythmValue == "2.2.2.2.2" || template.RhythmValue == "2.4.4")
        {
            var tail8 = clean.Substring(2); // 8 số đuôi
            if (tail8 == "86868686")
            {
                categories.Add(new SimCategoryFeature
                {
                    CategoryCode = "DAI_TAXI_86",
                    CategoryName = "Đại Taxi 4 Cặp Lộc Phát 86 (86.86.86.86)",
                    Multiplier = 10.0f,
                    ScoreContribution = 1.0f,
                    Description = "Siêu phẩm Đại Taxi 4 cặp Phát Lộc lặp lại liên tiếp 4 lần, nhận diện Hotline VIP đỉnh cao"
                });
                categories.Add(new SimCategoryFeature
                {
                    CategoryCode = "LOC_PHAT_86",
                    CategoryName = "Cặp Phát Lộc 86",
                    Multiplier = 2.2f,
                    ScoreContribution = 0.9f,
                    Description = "Cặp Bát Lục phát tài lộc hanh thông"
                });
            }
            else if (tail8 == "68686868")
            {
                categories.Add(new SimCategoryFeature
                {
                    CategoryCode = "DAI_TAXI_68",
                    CategoryName = "Đại Taxi 4 Cặp Lộc Phát 68 (68.68.68.68)",
                    Multiplier = 9.0f,
                    ScoreContribution = 1.0f,
                    Description = "Siêu phẩm Đại Taxi 4 cặp Lộc Phát liên hoàn cực kỳ quý hiếm"
                });
                categories.Add(new SimCategoryFeature
                {
                    CategoryCode = "LOC_PHAT_68",
                    CategoryName = "Cặp Lộc Phát 68",
                    Multiplier = 2.0f,
                    ScoreContribution = 0.9f,
                    Description = "Cặp Lục Bát tài lộc vượng khí"
                });
            }
            else if (tail8[0] == tail8[2] && tail8[2] == tail8[4] && tail8[4] == tail8[6] &&
                     tail8[1] == tail8[3] && tail8[3] == tail8[5] && tail8[5] == tail8[7] && tail8[0] != tail8[1])
            {
                categories.Add(new SimCategoryFeature
                {
                    CategoryCode = "DAI_TAXI_4_CAP",
                    CategoryName = $"Đại Taxi 4 Cặp ({tail8.Substring(0, 2)})",
                    Multiplier = 7.0f,
                    ScoreContribution = 0.95f,
                    Description = "Dạng Đại Taxi lặp 4 cặp số liên tiếp"
                });
            }
        }

        // 2. Bóc tách các tổ hợp số học & phong thủy chung
        ExtractCategoriesForString(clean, categories);

        // 3. Tính tổng điểm giá trị bộc lộ của nhịp: Score = BaseFactor * Product(1 + (Multiplier - 1) * Weight)
        double score = template.BaseFactor;
        foreach (var cat in categories)
        {
            score *= (1.0 + (cat.Multiplier - 1.0) * cat.ScoreContribution);
        }

        return new CadenceEvaluationResult
        {
            Template = template,
            FormattedNumber = formatted,
            Categories = categories,
            TotalScore = score
        };
    }

    private void ExtractCategoriesForString(string clean, List<SimCategoryFeature> categories)
    {
        // Tránh trùng lặp mã danh mục
        void AddIfNotExists(SimCategoryFeature feature)
        {
            if (!categories.Any(c => c.CategoryCode == feature.CategoryCode))
            {
                categories.Add(feature);
            }
        }

        // Bát quý (8 số đuôi giống nhau)
        if (clean.Length == 10 && clean.Substring(2) is var b8 && b8.All(c => c == b8[0]))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "BAT_QUY",
                CategoryName = $"Bát Quý {b8[0]}",
                Multiplier = 20.0f,
                ScoreContribution = 1.0f,
                Description = "Đỉnh cao tuyệt đối Bát Quý 8 số đồng nhất"
            });
        }

        // Lục quý (6 số đuôi giống nhau)
        if (Regex.IsMatch(clean, @"(\d)\1{5}$"))
        {
            var digit = clean[^1];
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "LUC_QUY",
                CategoryName = $"Lục Quý {digit}",
                Multiplier = (digit == '8' || digit == '9') ? 14.0f : 12.0f,
                ScoreContribution = 1.0f,
                Description = "6 chữ số cuối giống nhau liên tiếp"
            });
        }

        // Ngũ quý (5 số đuôi giống nhau)
        if (Regex.IsMatch(clean, @"(\d)\1{4}$"))
        {
            var digit = clean[^1];
            bool isVintagePrefix = clean.StartsWith("098") || clean.StartsWith("090") || clean.StartsWith("091") || clean.StartsWith("097");
            bool isSuperVipDigit = digit == '6' || digit == '8' || digit == '9';

            if (isVintagePrefix && isSuperVipDigit)
            {
                AddIfNotExists(new SimCategoryFeature
                {
                    CategoryCode = "NGU_QUY_VIP_DAU_CO",
                    CategoryName = $"Ngũ Quý {digit} Đầu Cổ VIP",
                    Multiplier = 18.0f,
                    ScoreContribution = 1.0f,
                    Description = $"Ngũ quý {digit} (Ngũ Đại Cát) kết hợp đầu số cổ Tam Đại Nhà Mạng đẳng cấp Siêu VIP tiền tỷ"
                });
            }
            else
            {
                AddIfNotExists(new SimCategoryFeature
                {
                    CategoryCode = "NGU_QUY",
                    CategoryName = $"Ngũ Quý {digit}",
                    Multiplier = isSuperVipDigit ? 10.0f : 7.0f,
                    ScoreContribution = 0.95f,
                    Description = "5 chữ số cuối giống nhau liên tiếp"
                });
            }
        }

        // Tứ quý (4 số đuôi giống nhau)
        if (Regex.IsMatch(clean, @"(\d)\1{3}$"))
        {
            var digit = clean[^1];
            bool isVintagePrefix = clean.StartsWith("098") || clean.StartsWith("090") || clean.StartsWith("091") || clean.StartsWith("097");
            bool isSuperVipDigit = digit == '6' || digit == '8' || digit == '9';

            if (isVintagePrefix && isSuperVipDigit)
            {
                AddIfNotExists(new SimCategoryFeature
                {
                    CategoryCode = "TU_QUY_VIP_DAU_CO",
                    CategoryName = $"Tứ Quý {digit} Đầu Cổ VIP",
                    Multiplier = 8.0f,
                    ScoreContribution = 0.95f,
                    Description = $"Tứ Quý {digit} kết hợp đầu cổ danh gia vọng tộc"
                });
            }
            else
            {
                AddIfNotExists(new SimCategoryFeature
                {
                    CategoryCode = "TU_QUY",
                    CategoryName = $"Tứ Quý {digit}",
                    Multiplier = isSuperVipDigit ? 5.5f : 4.0f,
                    ScoreContribution = 0.9f,
                    Description = "4 chữ số đuôi giống nhau biểu tượng quyền uy"
                });
            }
        }

        // Sảnh tiến Rồng 6 số (VD: 456789, 123456)
        if (IsConsecutiveSequence(clean, 6))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "SANH_TIEN_RONG",
                CategoryName = "Sảnh Tiến Rồng 6 Số",
                Multiplier = 5.0f,
                ScoreContribution = 0.95f,
                Description = "Sảnh rồng 6 bước thăng tiến sự nghiệp không ngừng"
            });
        }

        // Sảnh tiến 6789 ("San bằng tất cả")
        if (clean.EndsWith("6789"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "SANH_TIEN_6789",
                CategoryName = "Sảnh Tiến 6789 (San Bằng Tất Cả)",
                Multiplier = 4.5f,
                ScoreContribution = 0.9f,
                Description = "Dãy số kinh điển trong giới kinh doanh: Vượt qua mọi trở ngại"
            });
        }

        // Sinh Tài Lộc Phát: 1368
        if (clean.EndsWith("1368"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "CAT_TUONG_1368",
                CategoryName = "Sinh Tài Lộc Phát (1368)",
                Multiplier = 3.5f,
                ScoreContribution = 0.9f,
                Description = "Sinh Tài Lộc Phát trọn vẹn bốn mùa quanh năm"
            });
        }

        // Lộc Phát Kép (6868, 8686)
        if (clean.EndsWith("6868") || clean.EndsWith("8686"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "LOC_PHAT_KEP",
                CategoryName = "Lộc Phát Kép (6868/8686)",
                Multiplier = 3.5f,
                ScoreContribution = 0.85f,
                Description = "Đại lộc phát tài, nhân đôi vượng khí thương trường"
            });
        }
        else if (clean.EndsWith("86"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "LOC_PHAT_86",
                CategoryName = "Cặp Phát Lộc (86)",
                Multiplier = 2.2f,
                ScoreContribution = 0.8f,
                Description = "Cặp Phát Lộc Bát Lục tài lộc may mắn"
            });
        }
        else if (clean.EndsWith("68"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "LOC_PHAT_68",
                CategoryName = "Cặp Lộc Phát (68)",
                Multiplier = 2.0f,
                ScoreContribution = 0.8f,
                Description = "Cặp Lộc Phát Lục Bát vượng tài"
            });
        }

        // Thần Tài Kép (3979, 7939)
        if (clean.EndsWith("3979") || clean.EndsWith("7939"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "THAN_TAI_KEP",
                CategoryName = "Thần Tài Lớn Nhỏ Kép (3979)",
                Multiplier = 3.0f,
                ScoreContribution = 0.85f,
                Description = "Thần tài lớn nhỏ tương hỗ mang lại đại cát đại lợi"
            });
        }
        else if (clean.EndsWith("79"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "THAN_TAI_LON",
                CategoryName = "Thần Tài Lớn (79)",
                Multiplier = 2.2f,
                ScoreContribution = 0.8f,
                Description = "Thần tài lớn phù trợ kinh doanh buôn bán"
            });
        }
        else if (clean.EndsWith("39"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "THAN_TAI_NHO",
                CategoryName = "Thần Tài Nhỏ (39)",
                Multiplier = 1.8f,
                ScoreContribution = 0.75f,
                Description = "Thần tài nhỏ may mắn hanh thông"
            });
        }

        // Ông Địa Kép (3878, 7838)
        if (clean.EndsWith("3878") || clean.EndsWith("7838"))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "ONG_DIA_KEP",
                CategoryName = "Ông Địa Kép (3878)",
                Multiplier = 2.2f,
                ScoreContribution = 0.8f,
                Description = "Vượng khí đất đai, đắc lợi cho giới bất động sản"
            });
        }

        // Tam hoa kép (VD: 666888)
        if (clean.Length >= 6 && clean.Substring(clean.Length - 6) is var t6 &&
            t6[0] == t6[1] && t6[1] == t6[2] &&
            t6[3] == t6[4] && t6[4] == t6[5] && t6[0] != t6[3])
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "TAM_HOA_KEP",
                CategoryName = $"Tam Hoa Kép ({t6.Substring(0, 3)}.{t6.Substring(3, 3)})",
                Multiplier = 3.0f,
                ScoreContribution = 0.8f,
                Description = "Hai cụm tam hoa liên tiếp vững chãi như bàn thạch"
            });
        }

        // Sạch số không 4 và 7
        if (!clean.Contains('4') && !clean.Contains('7'))
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "KHONG_THAT_BAT",
                CategoryName = "Số Sạch Không 4-7 (Bất Tử - Bất Thất)",
                Multiplier = 1.35f,
                ScoreContribution = 0.7f,
                Description = "Số sạch hoàn toàn không dính 4 và 7, mang vượng khí thuần khiết"
            });
        }

        // Năm sinh (1950 - 2030)
        var last4 = clean.Length >= 4 ? clean.Substring(clean.Length - 4) : "";
        if (int.TryParse(last4, out int year) && year >= 1950 && year <= 2030)
        {
            AddIfNotExists(new SimCategoryFeature
            {
                CategoryCode = "SIM_NAM_SINH",
                CategoryName = $"SIM Năm Sinh {year}",
                Multiplier = 1.7f,
                ScoreContribution = 0.75f,
                Description = $"Gắn liền với năm sinh {year}, khẳng định dấu ấn cá nhân"
            });
        }
    }

    private void ExtractFengShuiModifiers(string clean, SimExtractedFeatures result)
    {
        if (clean.EndsWith("49") || clean.EndsWith("53") || clean.Contains("4953") || clean.Contains("5349"))
        {
            result.HasAvoidNumbers = true;
            result.FengShuiNotes = "Đuôi dính số hạn 49/53 theo quan niệm dân gian, cần chiết khấu giá.";
        }

        if (clean.EndsWith("8") || clean.EndsWith("9") || clean.EndsWith("68") || clean.EndsWith("86") || clean.EndsWith("79") || clean.EndsWith("39"))
        {
            result.HasLuckyEnd = true;
            if (string.IsNullOrEmpty(result.FengShuiNotes))
            {
                result.FengShuiNotes = "Đuôi kết thúc bằng số vượng khí tài lộc (8 - Phát, 9 - Cửu đỉnh, 68/86 - Lộc phát).";
            }
        }
    }

    private static string FormatWithPartitions(string clean, int[] partitions)
    {
        var sb = new StringBuilder();
        int offset = 0;
        for (int i = 0; i < partitions.Length; i++)
        {
            int len = partitions[i];
            if (offset + len <= clean.Length)
            {
                if (sb.Length > 0) sb.Append('.');
                sb.Append(clean.Substring(offset, len));
                offset += len;
            }
        }
        if (offset < clean.Length)
        {
            if (sb.Length > 0) sb.Append('.');
            sb.Append(clean.Substring(offset));
        }
        return sb.ToString();
    }

    private static bool IsConsecutiveSequence(string clean, int length)
    {
        if (clean.Length < length) return false;
        var sub = clean.Substring(clean.Length - length);
        for (int i = 0; i < sub.Length - 1; i++)
        {
            if (sub[i + 1] - sub[i] != 1) return false;
        }
        return true;
    }

    private static float CalculatePrefixReputationScore(string prefix)
    {
        return prefix switch
        {
            "098" or "097" or "096" => 1.5f, // Viettel cổ
            "090" or "093" => 1.45f,         // Mobi cổ
            "091" or "094" => 1.4f,          // Vina cổ
            "088" or "086" or "089" => 1.25f,// Đầu số Song phát / Đại phát
            "070" or "079" or "077" or "076" or "078" => 1.15f,
            "032" or "033" or "034" or "035" or "036" or "037" or "038" or "039" => 1.1f,
            "081" or "082" or "083" or "084" or "085" => 1.1f,
            "092" or "056" or "058" => 0.95f, // Vietnamobile
            "099" or "059" => 0.95f,          // Gmobile
            "087" or "055" => 0.9f,           // Wintel, Itelecom
            _ => 1.0f
        };
    }

    private static string DetectCarrierFromPrefix(string clean)
    {
        if (clean.Length < 3) return "Viễn thông Việt Nam";
        var p = clean.Substring(0, 3);
        if (p is "096" or "097" or "098" or "086" || (p.StartsWith("03") && clean.Length >= 3)) return "Viettel Telecom";
        if (p is "090" or "093" or "089" || (p.StartsWith("07") && clean.Length >= 3)) return "MobiFone";
        if (p is "091" or "094" or "088" || (p.StartsWith("08") && p != "086" && p != "089" && p != "087")) return "VinaPhone";
        if (p is "092" or "056" or "058") return "Vietnamobile";
        if (p is "099" or "059") return "Gmobile";
        if (p is "087") return "Wintel";
        if (p is "055") return "Reddi (FPT)";
        return "Viễn thông Việt Nam";
    }
}
