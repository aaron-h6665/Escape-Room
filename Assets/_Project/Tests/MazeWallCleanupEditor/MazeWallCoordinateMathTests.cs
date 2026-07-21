using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace MazeWallCleanup.Tests
{
    public sealed class MazeWallCoordinateMathTests
    {
        [Test]
        public void BuildTargets_AlignsPerpendicularWallEndpointAndCenterline()
        {
            List<MazeWallCoordinates> walls = new List<MazeWallCoordinates>
            {
                new MazeWallCoordinates(1, MazeWallAxis.Horizontal, 13.43f, 5.025f, 13.775f),
                new MazeWallCoordinates(2, MazeWallAxis.Vertical, 5f, 10f, 13.25f)
            };

            IReadOnlyList<MazeWallTarget> targets = MazeWallCoordinateMath.BuildTargets(walls, 0.25f);

            Assert.That(targets[0].Start, Is.EqualTo(5.0125f).Within(0.0001f));
            Assert.That(targets[1].Line, Is.EqualTo(5.0125f).Within(0.0001f));
            Assert.That(targets[0].Line, Is.EqualTo(13.34f).Within(0.0001f));
            Assert.That(targets[1].End, Is.EqualTo(13.34f).Within(0.0001f));
        }

        [Test]
        public void BuildTargets_LeavesCoordinatesOutsideToleranceUnchanged()
        {
            List<MazeWallCoordinates> walls = new List<MazeWallCoordinates>
            {
                new MazeWallCoordinates(1, MazeWallAxis.Vertical, 5f, 0f, 1f),
                new MazeWallCoordinates(2, MazeWallAxis.Vertical, 5.3f, 3f, 4f)
            };

            IReadOnlyList<MazeWallTarget> targets = MazeWallCoordinateMath.BuildTargets(walls, 0.25f);

            Assert.That(targets[0].Line, Is.EqualTo(5f));
            Assert.That(targets[1].Line, Is.EqualTo(5.3f));
        }

        [Test]
        public void BuildTargets_UsesMedianForAClusterConnectedByTolerance()
        {
            List<MazeWallCoordinates> walls = new List<MazeWallCoordinates>
            {
                new MazeWallCoordinates(1, MazeWallAxis.Vertical, -5.3f, 0f, 1f),
                new MazeWallCoordinates(2, MazeWallAxis.Vertical, -5.1f, 3f, 4f),
                new MazeWallCoordinates(3, MazeWallAxis.Vertical, -5f, 6f, 7f)
            };

            IReadOnlyList<MazeWallTarget> targets = MazeWallCoordinateMath.BuildTargets(walls, 0.25f);

            Assert.That(targets[0].Line, Is.EqualTo(-5.1f).Within(0.0001f));
            Assert.That(targets[1].Line, Is.EqualTo(-5.1f).Within(0.0001f));
            Assert.That(targets[2].Line, Is.EqualTo(-5.1f).Within(0.0001f));
        }

        [Test]
        public void BuildTargets_RejectsNegativeTolerance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MazeWallCoordinateMath.BuildTargets(Array.Empty<MazeWallCoordinates>(), -0.01f));
        }
    }
}
