using UnityEngine;

public static class CaesarCipherMath
{
    public const int NotchCount = 27;
    public const string Symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ?";
    public const float DegreesPerNotch = 360f / NotchCount;

    // Both rings use all 27 physical symbols. Spaces separate words, not notches.
    public static string ShiftSymbols(string text, int notches)
    {
        char[] result = text.ToUpperInvariant().ToCharArray();
        for (int i = 0; i < result.Length; i++)
        {
            int index = Symbols.IndexOf(result[i]);
            if (index >= 0) result[i] = IndexToSymbol(index + notches);
        }
        return new string(result);
    }

    public static char ReadOuterToInner(char outerSymbol, int clockwiseNotches)
    {
        int index = Symbols.IndexOf(char.ToUpperInvariant(outerSymbol));
        return index < 0 ? outerSymbol : IndexToSymbol(index - clockwiseNotches);
    }

    public static int NormalizeIndex(int index)
    {
        int normalized = index % NotchCount;
        return normalized < 0 ? normalized + NotchCount : normalized;
    }

    public static int SnapDegreesToIndex(float degrees)
    {
        return NormalizeIndex(Mathf.RoundToInt(degrees / DegreesPerNotch));
    }

    public static float IndexToDegrees(float index, float direction = 1f)
    {
        return index * DegreesPerNotch * Mathf.Sign(Mathf.Approximately(direction, 0f) ? 1f : direction);
    }

    public static char IndexToSymbol(int index)
    {
        return Symbols[NormalizeIndex(index)];
    }

    public static int TopSymbolIndex(int topSymbolAtZero, float rotationIndex, float rotationDirection = 1f)
    {
        int direction = Mathf.Approximately(rotationDirection, 0f) ? 1 : (int)Mathf.Sign(rotationDirection);
        return NormalizeIndex(topSymbolAtZero - Mathf.RoundToInt(rotationIndex) * direction);
    }

    public static char TopSymbol(int topSymbolAtZero, float rotationIndex, float rotationDirection = 1f)
    {
        return IndexToSymbol(TopSymbolIndex(topSymbolAtZero, rotationIndex, rotationDirection));
    }

    public static Quaternion RotationForIndex(Quaternion baseline, Vector3 parentLocalAxis, float index, float direction = 1f)
    {
        Vector3 axis = parentLocalAxis.sqrMagnitude > 0.0001f ? parentLocalAxis.normalized : Vector3.forward;
        return Quaternion.AngleAxis(IndexToDegrees(index, direction), axis) * baseline;
    }
}
