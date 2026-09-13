using System.Text.RegularExpressions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class SimFeatureExtractor : ISimFeatureExtractor
{
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

        // 1. Phân tích nhịp số (Format Rhythm)
        ExtractRhythm(clean, result);

        // 2. Phân tích danh mục phong thủy & số học (Sim Categories)
        ExtractCategories(clean, result);

        // 3. Phân tích yếu tố kiêng kỵ & may mắn
        ExtractFengShuiModifiers(clean, result);

        return result;
    }

    private void ExtractRhythm(string clean, SimExtractedFeatures result)
    {
        if (clean.Length < 6)
        {
            result.DetectedRhythmCode = "SIM_THUONG";
            result.DetectedRhythmName = "SIM Thường";
            result.RhythmBaseFactor = 1.0f;
            return;
        }

        // Lục quý (6 số cuối giống nhau)
        if (Regex.IsMatch(clean, @"(\d)\1{5}$"))
        {
            var digit = clean[^1];
            result.DetectedRhythmCode = "LUC_QUY";
            result.DetectedRhythmName = $"Lục quý {digit}";
            result.RhythmBaseFactor = (digit == '8' || digit == '9') ? 600.0f : (digit == '6' ? 500.0f : 400.0f);
            return;
        }

        // Ngũ quý (5 số cuối giống nhau)
        if (Regex.IsMatch(clean, @"(\d)\1{4}$"))
        {
            var digit = clean[^1];
            result.DetectedRhythmCode = "NGU_QUY";
            result.DetectedRhythmName = $"Ngũ quý {digit}";
            result.RhythmBaseFactor = (digit == '8' || digit == '9') ? 180.0f : (digit == '6' ? 150.0f : 100.0f);
            return;
        }

        // Tứ quý (4 số cuối giống nhau)
        if (Regex.IsMatch(clean, @"(\d)\1{3}$"))
        {
            var digit = clean[^1];
            result.DetectedRhythmCode = "TU_QUY";
            result.DetectedRhythmName = $"Tứ quý {digit}";
            result.RhythmBaseFactor = (digit == '8' || digit == '9') ? 60.0f : (digit == '6' ? 50.0f : 35.0f);
            return;
        }

        // Sảnh tiến 6 số (VD: 456789, 123456)
        if (IsConsecutiveSequence(clean, 6))
        {
            result.DetectedRhythmCode = "SANH_TIEN_6";
            result.DetectedRhythmName = "Sảnh tiến Rồng 6 số";
            result.RhythmBaseFactor = 80.0f;
            return;
        }

        // Sảnh tiến 5 số (VD: 56789, 12345)
        if (IsConsecutiveSequence(clean, 5))
        {
            result.DetectedRhythmCode = "SANH_TIEN_5";
            result.DetectedRhythmName = "Sảnh tiến 5 số";
            result.RhythmBaseFactor = 35.0f;
            return;
        }

        // Taxi 3 cặp (VD: 898989, 686868)
        if (clean.Length >= 6 && clean.Substring(clean.Length - 6) is var last6 &&
            last6[0] == last6[2] && last6[2] == last6[4] &&
            last6[1] == last6[3] && last6[3] == last6[5] && last6[0] != last6[1])
        {
            result.DetectedRhythmCode = "TAXI_3_CAP";
            result.DetectedRhythmName = "Taxi 3 Cặp";
            result.RhythmBaseFactor = 28.0f;
            return;
        }

        // Tam hoa kép (VD: 666888, 888999)
        if (clean.Length >= 6 && clean.Substring(clean.Length - 6) is var t6 &&
            t6[0] == t6[1] && t6[1] == t6[2] &&
            t6[3] == t6[4] && t6[4] == t6[5] && t6[0] != t6[3])
        {
            result.DetectedRhythmCode = "TAM_HOA_KEP";
            result.DetectedRhythmName = "Tam hoa kép";
            result.RhythmBaseFactor = 18.0f;
            return;
        }

        // Sảnh tiến 4 số (VD: 6789, 3456)
        if (IsConsecutiveSequence(clean, 4))
        {
            var isSanhCuoi = clean.EndsWith("6789");
            result.DetectedRhythmCode = "SANH_TIEN_4";
            result.DetectedRhythmName = isSanhCuoi ? "Sảnh tiến 6789 (San bằng tất cả)" : "Sảnh tiến 4 số";
            result.RhythmBaseFactor = isSanhCuoi ? 18.0f : 10.0f;
            return;
        }

        // Taxi 2 cặp (VD: 789789 hoặc 6868)
        if (clean.Length >= 6 && clean.Substring(clean.Length - 6) is var s6 &&
            s6.Substring(0, 3) == s6.Substring(3, 3))
        {
            result.DetectedRhythmCode = "TAXI_2_CAP";
            result.DetectedRhythmName = "Taxi ABC.ABC";
            result.RhythmBaseFactor = 12.0f;
            return;
        }

        // Tam hoa đuôi (VD: 777, 888, 999)
        if (Regex.IsMatch(clean, @"(\d)\1{2}$"))
        {
            var digit = clean[^1];
            result.DetectedRhythmCode = "TAM_HOA";
            result.DetectedRhythmName = $"Tam hoa {digit}";
            result.RhythmBaseFactor = (digit == '8' || digit == '9') ? 6.0f : (digit == '6' ? 5.0f : 3.5f);
            return;
        }

        // Lặp đôi / Gánh đảo (VD: ABAB, ABA)
        if (clean.Length >= 4 && clean.Substring(clean.Length - 4) is var d4 &&
            d4[0] == d4[2] && d4[1] == d4[3] && d4[0] != d4[1])
        {
            result.DetectedRhythmCode = "LAP_DOI";
            result.DetectedRhythmName = "Lặp đôi AB.AB";
            result.RhythmBaseFactor = 3.0f;
            return;
        }

        if (clean.Length >= 3 && clean.Substring(clean.Length - 3) is var g3 &&
            g3[0] == g3[2] && g3[0] != g3[1])
        {
            result.DetectedRhythmCode = "GANH_DAO";
            result.DetectedRhythmName = "Gánh đảo ABA";
            result.RhythmBaseFactor = 2.0f;
            return;
        }

        result.DetectedRhythmCode = "SIM_THUONG";
        result.DetectedRhythmName = "SIM Dễ nhớ";
        result.RhythmBaseFactor = 1.0f;
    }

    private void ExtractCategories(string clean, SimExtractedFeatures result)
    {
        // 1. Cặp Lộc Phát (68, 86, 6868)
        if (clean.EndsWith("6868") || clean.EndsWith("8686"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "LOC_PHAT_KEP",
                CategoryName = "Lộc Phát kép (6868/8686)",
                Multiplier = 3.5f,
                ScoreContribution = 1.0f,
                Description = "Đại lộc phát tài, nhân đôi vượng khí cho gia chủ kinh doanh"
            });
        }
        else if (clean.EndsWith("68") || clean.EndsWith("86"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "LOC_PHAT",
                CategoryName = "Lộc Phát (68 - 86)",
                Multiplier = 2.0f,
                ScoreContribution = 0.8f,
                Description = "Đuôi Lộc phát kích hoạt tài lộc may mắn"
            });
        }

        // 2. Cặp Thần Tài (39, 79, 3979, 7939)
        if (clean.EndsWith("3979") || clean.EndsWith("7939"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "THAN_TAI_KEP",
                CategoryName = "Thần Tài lớn nhỏ kết hợp (3979)",
                Multiplier = 3.0f,
                ScoreContribution = 1.0f,
                Description = "Thần tài gõ cửa mang lại may mắn lớn trong công danh tài chính"
            });
        }
        else if (clean.EndsWith("79"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "THAN_TAI_LON",
                CategoryName = "Thần Tài Lớn (79)",
                Multiplier = 2.2f,
                ScoreContribution = 0.85f,
                Description = "Thần tài lớn phù trợ thương trường"
            });
        }
        else if (clean.EndsWith("39"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "THAN_TAI_NHO",
                CategoryName = "Thần Tài Nhỏ (39)",
                Multiplier = 1.8f,
                ScoreContribution = 0.8f,
                Description = "Thần tài nhỏ tài lộc hanh thông"
            });
        }

        // 3. Cặp Ông Địa (38, 78, 3878)
        if (clean.EndsWith("3878") || clean.EndsWith("7838") || clean.EndsWith("7878"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "ONG_DIA_KEP",
                CategoryName = "Ông Địa Kép (3878)",
                Multiplier = 2.2f,
                ScoreContribution = 0.9f,
                Description = "Rất tốt cho người làm bất động sản, đất đai, nhà ở"
            });
        }
        else if (clean.EndsWith("78") || clean.EndsWith("38"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "ONG_DIA",
                CategoryName = "Ông Địa (38 - 78)",
                Multiplier = 1.6f,
                ScoreContribution = 0.75f,
                Description = "Vượng khí điền trạch bất động sản"
            });
        }

        // 4. Cát tường đặc biệt: 1368 (Sinh tài lộc phát)
        if (clean.EndsWith("1368"))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "CAT_TUONG_1368",
                CategoryName = "Sinh Tài Lộc Phát (1368)",
                Multiplier = 3.2f,
                ScoreContribution = 1.0f,
                Description = "Dãy số phong thủy cát tường đỉnh cao: Sinh Tài Lộc Phát quanh năm"
            });
        }

        // 5. Năm sinh (VD: 1980 - 2025)
        var last4 = clean.Length >= 4 ? clean.Substring(clean.Length - 4) : "";
        if (int.TryParse(last4, out int year) && year >= 1950 && year <= 2030)
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "SIM_NAM_SINH",
                CategoryName = $"SIM Năm Sinh {year}",
                Multiplier = 1.7f,
                ScoreContribution = 0.8f,
                Description = $"Gắn liền với dấu mốc năm sinh {year}, mang dấu ấn cá nhân đậm nét"
            });
        }

        // 6. SIM Phong thủy không chứa 4 và 7 (Không thất bát, không tử)
        if (!clean.Contains('4') && !clean.Contains('7'))
        {
            result.DetectedCategories.Add(new SimCategoryFeature
            {
                CategoryCode = "KHONG_THAT_BAT",
                CategoryName = "SIM Không 4 - 7 (Sạch số cát khí)",
                Multiplier = 1.35f,
                ScoreContribution = 0.7f,
                Description = "Số sạch hoàn toàn không dính số 4 (Tử) và 7 (Thất), vượng khí thuần khiết"
            });
        }
    }

    private void ExtractFengShuiModifiers(string clean, SimExtractedFeatures result)
    {
        // Kiêng kỵ đuôi 49, 53 hoặc chứa cặp 4953
        if (clean.EndsWith("49") || clean.EndsWith("53") || clean.Contains("4953") || clean.Contains("5349"))
        {
            result.HasAvoidNumbers = true;
            result.FengShuiNotes = "Đuôi dính số hạn 49/53 theo quan niệm dân gian, cần chiết khấu giá.";
        }

        // May mắn đuôi 8, 9, 68, 79
        if (clean.EndsWith("8") || clean.EndsWith("9") || clean.EndsWith("68") || clean.EndsWith("79") || clean.EndsWith("88") || clean.EndsWith("99"))
        {
            result.HasLuckyEnd = true;
            if (string.IsNullOrEmpty(result.FengShuiNotes))
            {
                result.FengShuiNotes = "Đuôi kết thúc bằng số vượng khí (8 - Bát Bạch phát tài, 9 - Cửu Tử trường tồn).";
            }
        }
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
