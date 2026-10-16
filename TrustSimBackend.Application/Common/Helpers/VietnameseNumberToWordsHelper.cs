using System.Text;

namespace TrustSimBackend.Application.Common.Helpers;

/// <summary>
/// Tiện ích chuyển đổi số tiền thành chữ tiếng Việt chuẩn xác phục vụ in ấn hợp đồng, biên bản pháp lý.
/// </summary>
public static class VietnameseNumberToWordsHelper
{
    private static readonly string[] Digits = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
    private static readonly string[] Groups = { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };

    /// <summary>
    /// Chuyển đổi số tiền decimal thành văn bản tiếng Việt có viết hoa chữ cái đầu và hậu tố đơn vị tiền tệ.
    /// Ví dụ: 15000000 -> "Mười lăm triệu đồng chẵn"
    /// </summary>
    public static string ToVietnameseWords(decimal amount, string currency = "đồng chẵn")
    {
        if (amount == 0)
        {
            return $"Không {currency}".Trim();
        }

        if (amount < 0)
        {
            return "Âm " + ToVietnameseWords(Math.Abs(amount), currency);
        }

        long integralPart = (long)Math.Floor(amount);
        if (integralPart == 0)
        {
            return $"Không {currency}".Trim();
        }

        var groups = new List<int>();
        long temp = integralPart;
        while (temp > 0)
        {
            groups.Add((int)(temp % 1000));
            temp /= 1000;
        }

        var sb = new StringBuilder();

        for (int i = groups.Count - 1; i >= 0; i--)
        {
            int number = groups[i];
            if (number == 0) continue;

            string groupWords = ReadThreeDigits(number, i < groups.Count - 1);
            if (!string.IsNullOrWhiteSpace(groupWords))
            {
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(groupWords);

                string groupUnit = Groups[i];
                if (!string.IsNullOrWhiteSpace(groupUnit))
                {
                    sb.Append(" ").Append(groupUnit);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            sb.Append(" ").Append(currency);
        }

        string result = sb.ToString().Trim();
        if (result.Length > 0)
        {
            result = char.ToUpper(result[0]) + result.Substring(1);
        }

        return result;
    }

    private static string ReadThreeDigits(int number, bool readZeroHundreds)
    {
        int hundreds = number / 100;
        int tens = (number % 100) / 10;
        int units = number % 10;

        var sb = new StringBuilder();

        if (hundreds > 0 || readZeroHundreds)
        {
            sb.Append(Digits[hundreds]).Append(" trăm");
        }

        if (tens > 1)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append(Digits[tens]).Append(" mươi");
            if (units == 1)
            {
                sb.Append(" mốt");
            }
            else if (units == 5)
            {
                sb.Append(" lăm");
            }
            else if (units > 0)
            {
                sb.Append(" ").Append(Digits[units]);
            }
        }
        else if (tens == 1)
        {
            if (sb.Length > 0) sb.Append(" ");
            sb.Append("mười");
            if (units == 5)
            {
                sb.Append(" lăm");
            }
            else if (units > 0)
            {
                sb.Append(" ").Append(Digits[units]);
            }
        }
        else // tens == 0
        {
            if (units > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" lẻ ").Append(Digits[units]);
                }
                else
                {
                    sb.Append(Digits[units]);
                }
            }
        }

        return sb.ToString().Trim();
    }
}
