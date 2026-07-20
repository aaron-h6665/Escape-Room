using UnityEngine;

public static class CaesarCipherMath
{
    public const int NotchCount = 27;
    public const string Symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ?";
    public const float DegreesPerNotch = 360f / NotchCount;

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

    public static Quaternion RotationForIndex(Quaternion baseline, Vector3 parentLocalAxis, float index, float direction = 1f)
    {
        Vector3 axis = parentLocalAxis.sqrMagnitude > 0.0001f ? parentLocalAxis.normalized : Vector3.forward;
        return Quaternion.AngleAxis(IndexToDegrees(index, direction), axis) * baseline;
    }
}
