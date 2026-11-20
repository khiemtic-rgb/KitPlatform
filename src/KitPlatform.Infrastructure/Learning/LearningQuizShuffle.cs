using System.Security.Cryptography;

namespace KitPlatform.Infrastructure.Learning;

/// <summary>
/// Xáo đáp án + thứ tự câu hỏi cố định theo (user, câu hỏi, lần làm bài).
/// Bảng gốc (correct_option_index) không đổi; chỉ đổi thứ tự hiển thị và map ngược khi chấm.
/// </summary>
internal static class LearningQuizShuffle
{
    /// <summary>order[displayIndex] = canonicalIndex.</summary>
    public static int[] OptionOrder(Guid userId, Guid questionId, int variant, int optionCount) =>
        Permutation(Seed(userId, questionId, variant, 'o'), optionCount);

    /// <summary>order[displayPosition] = canonicalPosition (theo sort_order).</summary>
    public static int[] QuestionOrder(Guid userId, Guid moduleId, int variant, int questionCount) =>
        Permutation(Seed(userId, moduleId, variant, 'q'), questionCount);

    public static IReadOnlyList<T> ApplyOrder<T>(IReadOnlyList<T> canonical, int[] order) =>
        order.Select(i => canonical[i]).ToList();

    /// <summary>-1 nếu index hiển thị ngoài phạm vi.</summary>
    public static int ToCanonical(int[] order, int displayIndex) =>
        displayIndex >= 0 && displayIndex < order.Length ? order[displayIndex] : -1;

    private static ulong Seed(Guid userId, Guid itemId, int variant, char purpose)
    {
        Span<byte> input = stackalloc byte[37];
        userId.TryWriteBytes(input[..16]);
        itemId.TryWriteBytes(input.Slice(16, 16));
        BitConverter.TryWriteBytes(input.Slice(32, 4), variant);
        input[36] = (byte)purpose;
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return BitConverter.ToUInt64(hash[..8]);
    }

    private static int[] Permutation(ulong seed, int count)
    {
        var order = Enumerable.Range(0, Math.Max(0, count)).ToArray();
        var state = seed;
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = (int)(SplitMix64(ref state) % (ulong)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }

    private static ulong SplitMix64(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
