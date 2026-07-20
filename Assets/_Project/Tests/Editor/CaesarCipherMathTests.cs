using NUnit.Framework;
using UnityEngine;

public sealed class CaesarCipherMathTests
{
    [TestCase(-1, 26)]
    [TestCase(27, 0)]
    [TestCase(55, 1)]
    [TestCase(-55, 26)]
    public void NormalizeIndex_WrapsAcrossAllTwentySevenNotches(int input, int expected)
    {
        Assert.That(CaesarCipherMath.NormalizeIndex(input), Is.EqualTo(expected));
    }

    [Test]
    public void DegreesPerNotch_IsThirteenAndOneThirdDegrees()
    {
        Assert.That(CaesarCipherMath.DegreesPerNotch, Is.EqualTo(13.333333f).Within(0.0001f));
    }

    [Test]
    public void SnapDegreesToIndex_RoundsAtTheHalfNotchBoundary()
    {
        float belowMidpoint = CaesarCipherMath.DegreesPerNotch * 0.49f;
        float aboveMidpoint = CaesarCipherMath.DegreesPerNotch * 0.51f;
        Assert.That(CaesarCipherMath.SnapDegreesToIndex(belowMidpoint), Is.EqualTo(0));
        Assert.That(CaesarCipherMath.SnapDegreesToIndex(aboveMidpoint), Is.EqualTo(1));
    }

    [Test]
    public void IndexToSymbol_MapsFinalNotchToQuestionMark()
    {
        Assert.That(CaesarCipherMath.IndexToSymbol(26), Is.EqualTo('?'));
        Assert.That(CaesarCipherMath.IndexToSymbol(-1), Is.EqualTo('?'));
    }

    [Test]
    public void TopSymbol_UsesPhysicalBaselineAndRotationDirection()
    {
        Assert.That(CaesarCipherMath.TopSymbol(13, 0f), Is.EqualTo('N'));
        Assert.That(CaesarCipherMath.TopSymbol(13, 1f), Is.EqualTo('O'));
        Assert.That(CaesarCipherMath.TopSymbol(13, -1f), Is.EqualTo('M'));
        Assert.That(CaesarCipherMath.TopSymbol(13, 1f, -1f), Is.EqualTo('M'));
    }

    [Test]
    public void RotationForIndex_IsCalculatedFromBaselineInsteadOfAccumulating()
    {
        Quaternion baseline = Quaternion.Euler(7f, 13f, 19f);
        Quaternion first = CaesarCipherMath.RotationForIndex(baseline, Vector3.up, 11f);
        Quaternion second = CaesarCipherMath.RotationForIndex(baseline, Vector3.up, 11f);
        Assert.That(Quaternion.Angle(first, second), Is.LessThan(0.0001f));
    }
}
