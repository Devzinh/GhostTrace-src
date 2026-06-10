using System;
using System.Text;

namespace GhostTrace.Modules.Common;

/// <summary>
/// ROT13 transform. Windows obfuscates UserAssist value names with ROT13, so decoding
/// is required to recover the original executable / shortcut paths.
/// ROT13 is its own inverse, so the same method encodes and decodes.
/// </summary>
public static class Rot13
{
    public static string Transform(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            char shifted = c switch
            {
                >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
                >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
                _ => c
            };
            sb.Append(shifted);
        }
        return sb.ToString();
    }
}
