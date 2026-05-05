using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlazorWasm.Services;

namespace TestProject1
{
    /// <summary>
    /// Unit tests for Bounce physics simulation
    /// Tests vector operations, collision detection, and physics calculations
    /// </summary>
    [TestClass]
    public class TestBouncePhysics
    {
        private RandomService _randomService = null!;
        private Random _random = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            DebugHelper.SetDebugMode(true);
            _randomService = new RandomService();
            _randomService.Reset();
            _random = _randomService.GetRandom();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            DebugHelper.SetDebugMode(false);
        }

        #region Vector2D Tests

        // Simplified Vector2D for testing
        private record struct Vector2D(double X, double Y)
        {
            public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
            public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
            public static Vector2D operator *(Vector2D v, double scalar) => new(v.X * scalar, v.Y * scalar);
            public static Vector2D operator /(Vector2D v, double scalar) => new(v.X / scalar, v.Y / scalar);
            public static Vector2D operator -(Vector2D v) => new(-v.X, -v.Y);

            public double Length => Math.Sqrt(X * X + Y * Y);
            public Vector2D Normalized => Length > 0 ? this / Length : this;
            public double Dot(Vector2D other) => X * other.X + Y * other.Y;
        }

        [TestMethod]
        public void Vector2D_Addition_WorksCorrectly()
        {
            var v1 = new Vector2D(3, 4);
            var v2 = new Vector2D(1, 2);
            var result = v1 + v2;
            Assert.AreEqual(4, result.X);
            Assert.AreEqual(6, result.Y);
        }

        [TestMethod]
        public void Vector2D_Subtraction_WorksCorrectly()
        {
            var v1 = new Vector2D(5, 7);
            var v2 = new Vector2D(2, 3);
            var result = v1 - v2;
            Assert.AreEqual(3, result.X);
            Assert.AreEqual(4, result.Y);
        }

        [TestMethod]
        public void Vector2D_ScalarMultiplication_WorksCorrectly()
        {
            var v = new Vector2D(2, 3);
            double scalar = 3;
            var result = v * scalar;
            Assert.AreEqual(6, result.X);
            Assert.AreEqual(9, result.Y);
        }

        [TestMethod]
        public void Vector2D_ScalarDivision_WorksCorrectly()
        {
            var v = new Vector2D(10, 20);
            double scalar = 2;
            var result = v / scalar;
            Assert.AreEqual(5, result.X);
            Assert.AreEqual(10, result.Y);
        }

        [TestMethod]
        public void Vector2D_Negation_WorksCorrectly()
        {
            var v = new Vector2D(3, -4);
            var result = -v;
            Assert.AreEqual(-3, result.X);
            Assert.AreEqual(4, result.Y);
        }

        [TestMethod]
        public void Vector2D_Length_CalculatesCorrectly()
        {
            var v = new Vector2D(3, 4);
            var length = v.Length;
            Assert.AreEqual(5, length, 0.001, "Length of (3,4) should be 5");
        }

        [TestMethod]
        public void Vector2D_Normalized_CreatesUnitVector()
        {
            var v = new Vector2D(3, 4);
            var normalized = v.Normalized;
            Assert.AreEqual(1, normalized.Length, 0.001, "Normalized vector should have length 1");
            Assert.AreEqual(0.6, normalized.X, 0.001, "X component should be 3/5");
            Assert.AreEqual(0.8, normalized.Y, 0.001, "Y component should be 4/5");
        }

        [TestMethod]
        public void Vector2D_DotProduct_CalculatesCorrectly()
        {
            var v1 = new Vector2D(2, 3);
            var v2 = new Vector2D(4, 5);
            var dot = v1.Dot(v2);
            Assert.AreEqual(23, dot, 0.001, "Dot product should be 2*4 + 3*5 = 23");
        }

        [TestMethod]
        public void Vector2D_DotProduct_Perpendicular_IsZero()
        {
            var v1 = new Vector2D(1, 0);
            var v2 = new Vector2D(0, 1);
            var dot = v1.Dot(v2);
            Assert.AreEqual(0, dot, 0.001, "Perpendicular vectors have dot product 0");
        }

        #endregion

        #region Ball Physics Tests

        private record struct Ball(Vector2D Position, Vector2D Velocity, double Radius, string Color);

        [TestMethod]
        public void Ball_Creation_InitializesCorrectly()
        {
            var ball = new Ball(new Vector2D(100, 200), new Vector2D(5, -3), 10, "#FF0000");
            Assert.AreEqual(100, ball.Position.X);
            Assert.AreEqual(200, ball.Position.Y);
            Assert.AreEqual(5, ball.Velocity.X);
            Assert.AreEqual(-3, ball.Velocity.Y);
            Assert.AreEqual(10, ball.Radius);
            Assert.AreEqual("#FF0000", ball.Color);
        }

        [TestMethod]
        public void Ball_Movement_UpdatesPosition()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(10, 5), 10, "#FF0000");
            double timeScale = 1.0;
            var newPosition = ball.Position + ball.Velocity * timeScale;
            ball = ball with { Position = newPosition };
            Assert.AreEqual(110, ball.Position.X);
            Assert.AreEqual(105, ball.Position.Y);
        }

        [TestMethod]
        public void Ball_Gravity_IncreasesDownwardVelocity()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(0, 0), 10, "#FF0000");
            double gravity = 0.5;
            double timeScale = 1.0;
            var gravityForce = new Vector2D(0, gravity * timeScale);
            var newVelocity = ball.Velocity + gravityForce;
            ball = ball with { Velocity = newVelocity };
            Assert.AreEqual(0, ball.Velocity.X);
            Assert.AreEqual(0.5, ball.Velocity.Y, 0.001);
        }

        [TestMethod]
        public void Ball_AirResistance_SlowsDown()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(10, 5), 10, "#FF0000");
            double airResistance = 0.99;
            var newVelocity = ball.Velocity * airResistance;
            ball = ball with { Velocity = newVelocity };
            Assert.AreEqual(9.9, ball.Velocity.X, 0.001);
            Assert.AreEqual(4.95, ball.Velocity.Y, 0.001);
        }

        #endregion

        #region Wall Collision Tests

        [TestMethod]
        public void Ball_LeftWallCollision_ReversesXVelocity()
        {
            var ball = new Ball(new Vector2D(5, 100), new Vector2D(-10, 0), 10, "#FF0000");
            double elasticity = 0.8;

            bool hitLeftWall = ball.Position.X - ball.Radius < 0;
            if (hitLeftWall)
            {
                var newPosition = new Vector2D(ball.Radius, ball.Position.Y);
                var newVelocity = new Vector2D(-ball.Velocity.X * elasticity, ball.Velocity.Y);
                ball = ball with { Position = newPosition, Velocity = newVelocity };
            }

            Assert.AreEqual(10, ball.Position.X, "Ball should be moved to left boundary");
            Assert.AreEqual(8, ball.Velocity.X, 0.001, "X velocity should reverse and reduce");
        }

        [TestMethod]
        public void Ball_RightWallCollision_ReversesXVelocity()
        {
            double canvasWidth = 1200;
            var ball = new Ball(new Vector2D(1195, 100), new Vector2D(10, 0), 10, "#FF0000");
            double elasticity = 0.8;

            bool hitRightWall = ball.Position.X + ball.Radius > canvasWidth;
            if (hitRightWall)
            {
                var newPosition = new Vector2D(canvasWidth - ball.Radius, ball.Position.Y);
                var newVelocity = new Vector2D(-ball.Velocity.X * elasticity, ball.Velocity.Y);
                ball = ball with { Position = newPosition, Velocity = newVelocity };
            }

            Assert.AreEqual(1190, ball.Position.X, "Ball should be moved to right boundary");
            Assert.AreEqual(-8, ball.Velocity.X, 0.001, "X velocity should reverse and reduce");
        }

        [TestMethod]
        public void Ball_TopWallCollision_ReversesYVelocity()
        {
            var ball = new Ball(new Vector2D(100, 5), new Vector2D(0, -10), 10, "#FF0000");
            double elasticity = 0.8;

            bool hitTopWall = ball.Position.Y - ball.Radius < 0;
            if (hitTopWall)
            {
                var newPosition = new Vector2D(ball.Position.X, ball.Radius);
                var newVelocity = new Vector2D(ball.Velocity.X, -ball.Velocity.Y * elasticity);
                ball = ball with { Position = newPosition, Velocity = newVelocity };
            }

            Assert.AreEqual(10, ball.Position.Y, "Ball should be moved to top boundary");
            Assert.AreEqual(8, ball.Velocity.Y, 0.001, "Y velocity should reverse and reduce");
        }

        [TestMethod]
        public void Ball_BottomWallCollision_ReversesYVelocity()
        {
            double canvasHeight = 800;
            var ball = new Ball(new Vector2D(100, 795), new Vector2D(0, 10), 10, "#FF0000");
            double elasticity = 0.8;

            bool hitBottomWall = ball.Position.Y + ball.Radius > canvasHeight;
            if (hitBottomWall)
            {
                var newPosition = new Vector2D(ball.Position.X, canvasHeight - ball.Radius);
                var newVelocity = new Vector2D(ball.Velocity.X, -ball.Velocity.Y * elasticity);
                ball = ball with { Position = newPosition, Velocity = newVelocity };
            }

            Assert.AreEqual(790, ball.Position.Y, "Ball should be moved to bottom boundary");
            Assert.AreEqual(-8, ball.Velocity.Y, 0.001, "Y velocity should reverse and reduce");
        }

        [TestMethod]
        public void Ball_PerfectElasticity_NoEnergyLoss()
        {
            var ball = new Ball(new Vector2D(5, 100), new Vector2D(-10, 0), 10, "#FF0000");
            double elasticity = 1.0;
            var newVelocity = new Vector2D(-ball.Velocity.X * elasticity, ball.Velocity.Y);
            Assert.AreEqual(10, newVelocity.X, "With perfect elasticity, speed should be maintained");
        }

        [TestMethod]
        public void Ball_ZeroElasticity_StopsOnCollision()
        {
            var ball = new Ball(new Vector2D(5, 100), new Vector2D(-10, 0), 10, "#FF0000");
            double elasticity = 0.0;
            var newVelocity = new Vector2D(-ball.Velocity.X * elasticity, ball.Velocity.Y);
            Assert.AreEqual(0, newVelocity.X, "With zero elasticity, ball should stop");
        }

        #endregion

        #region Ball-to-Ball Collision Tests

        [TestMethod]
        public void Ball_CollisionDetection_DetectsOverlap()
        {
            var ball1 = new Ball(new Vector2D(100, 100), new Vector2D(0, 0), 10, "#FF0000");
            var ball2 = new Ball(new Vector2D(115, 100), new Vector2D(0, 0), 10, "#0000FF");

            var delta = ball2.Position - ball1.Position;
            var distance = delta.Length;
            var minDistance = ball1.Radius + ball2.Radius;
            bool isColliding = distance < minDistance;

            Assert.AreEqual(15, distance, 0.001, "Distance between centers");
            Assert.AreEqual(20, minDistance, "Sum of radii");
            Assert.IsTrue(isColliding, "Balls should be colliding");
        }

        [TestMethod]
        public void Ball_CollisionDetection_NoOverlapWhenFarApart()
        {
            var ball1 = new Ball(new Vector2D(100, 100), new Vector2D(0, 0), 10, "#FF0000");
            var ball2 = new Ball(new Vector2D(150, 100), new Vector2D(0, 0), 10, "#0000FF");

            var delta = ball2.Position - ball1.Position;
            var distance = delta.Length;
            var minDistance = ball1.Radius + ball2.Radius;
            bool isColliding = distance < minDistance;

            Assert.AreEqual(50, distance, 0.001);
            Assert.IsFalse(isColliding, "Balls should not be colliding");
        }

        [TestMethod]
        public void Ball_ElasticCollision_ExchangesVelocities()
        {
            var ball1 = new Ball(new Vector2D(100, 100), new Vector2D(5, 0), 10, "#FF0000");
            var ball2 = new Ball(new Vector2D(115, 100), new Vector2D(-3, 0), 10, "#0000FF");
            double elasticity = 1.0;

            var delta = ball2.Position - ball1.Position;
            var normal = delta.Normalized;

            var v1n = ball1.Velocity.Dot(normal);
            var v2n = ball2.Velocity.Dot(normal);

            var v1nNew = v2n * elasticity;
            var v2nNew = v1n * elasticity;

            Assert.IsTrue(v1nNew < v1n, "Ball1 should slow down or reverse");
            Assert.IsTrue(v2nNew > v2n, "Ball2 should slow down or reverse");
        }

        [TestMethod]
        public void Ball_CollisionNormal_PointsFromBall1ToBall2()
        {
            var ball1 = new Vector2D(100, 100);
            var ball2 = new Vector2D(120, 100);

            var delta = ball2 - ball1;
            var normal = delta.Normalized;

            Assert.AreEqual(1, normal.X, 0.001, "Normal should point right (from ball1 to ball2)");
            Assert.AreEqual(0, normal.Y, 0.001, "No Y component for horizontal collision");
        }

        [TestMethod]
        public void Ball_TangentVector_PerpendicularToNormal()
        {
            var normal = new Vector2D(1, 0);
            var tangent = new Vector2D(-normal.Y, normal.X);

            Assert.AreEqual(0, tangent.X, 0.001);
            Assert.AreEqual(1, tangent.Y, 0.001, "Tangent should be perpendicular to normal");
            Assert.AreEqual(0, normal.Dot(tangent), 0.001, "Dot product of perpendicular vectors is 0");
        }

        [TestMethod]
        public void Ball_CollisionSeparation_PreventsOverlap()
        {
            var ball1 = new Ball(new Vector2D(100, 100), new Vector2D(5, 0), 10, "#FF0000");
            var ball2 = new Ball(new Vector2D(115, 100), new Vector2D(-3, 0), 10, "#0000FF");

            var delta = ball2.Position - ball1.Position;
            var distance = delta.Length;
            var minDistance = ball1.Radius + ball2.Radius;
            var overlap = minDistance - distance;
            var normal = delta.Normalized;

            var separation = normal * (overlap * 0.5);
            var newPos1 = ball1.Position - separation;
            var newPos2 = ball2.Position + separation;

            var newDelta = newPos2 - newPos1;
            var newDistance = newDelta.Length;
            Assert.AreEqual(minDistance, newDistance, 0.001, "Balls should no longer overlap");
        }

        #endregion

        #region Physics Integration Tests

        [TestMethod]
        public void Ball_FreeFall_AcceleratesDownward()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(0, 0), 10, "#FF0000");
            double gravity = 0.5;
            double timeScale = 1.0;

            for (int i = 0; i < 5; i++)
            {
                var gravityForce = new Vector2D(0, gravity * timeScale);
                var newVelocity = ball.Velocity + gravityForce;
                var newPosition = ball.Position + newVelocity * timeScale;
                ball = ball with { Velocity = newVelocity, Position = newPosition };
            }

            Assert.AreEqual(0, ball.Velocity.X, "No horizontal velocity");
            Assert.AreEqual(2.5, ball.Velocity.Y, 0.001, "Vertical velocity should be 5 * 0.5");
            Assert.IsTrue(ball.Position.Y > 100, "Ball should have fallen");
        }

        [TestMethod]
        public void Ball_ProjectileMotion_FollowsParabola()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(5, -10), 10, "#FF0000");
            double gravity = 1.0;
            double timeScale = 1.0;

            double minVelocityY = double.MaxValue;

            for (int i = 0; i < 20; i++)
            {
                var gravityForce = new Vector2D(0, gravity * timeScale);
                var newVelocity = ball.Velocity + gravityForce;
                var newPosition = ball.Position + newVelocity * timeScale;
                ball = ball with { Velocity = newVelocity, Position = newPosition };

                minVelocityY = Math.Min(minVelocityY, Math.Abs(ball.Velocity.Y));
            }

            Assert.IsTrue(ball.Position.X > 100, "Ball should have moved horizontally");
            Assert.AreEqual(10, ball.Velocity.Y, 0.001, "Y velocity should return to initial (opposite direction)");
            Assert.IsTrue(minVelocityY < 1, $"At apex, Y velocity should be near 0, was {minVelocityY:F2}");
        }

        [TestMethod]
        public void Ball_BouncingBall_LosesEnergyOverTime()
        {
            double canvasHeight = 800;
            var ball = new Ball(new Vector2D(400, 100), new Vector2D(0, 0), 10, "#FF0000");
            double gravity = 1.0;
            double elasticity = 0.8;
            double timeScale = 1.0;

            var bounceHeights = new List<double>();
            bool wasFalling = false;
            double peakHeight = ball.Position.Y;

            for (int frame = 0; frame < 200; frame++)
            {
                var gravityForce = new Vector2D(0, gravity * timeScale);
                var newVelocity = ball.Velocity + gravityForce;
                var newPosition = ball.Position + newVelocity * timeScale;

                bool isFalling = newVelocity.Y > 0;

                if (isFalling && !wasFalling)
                {
                    bounceHeights.Add(peakHeight);
                    Console.WriteLine($"  ?? Bounce detected at peak Y={peakHeight:F1}");
                    peakHeight = newPosition.Y;
                }
                else if (isFalling)
                {
                    peakHeight = newPosition.Y;
                }
                else
                {
                    peakHeight = Math.Min(peakHeight, newPosition.Y);
                }

                wasFalling = isFalling;

                if (newPosition.Y + ball.Radius > canvasHeight)
                {
                    newPosition = new Vector2D(newPosition.X, canvasHeight - ball.Radius);
                    newVelocity = new Vector2D(newVelocity.X, -newVelocity.Y * elasticity);
                }

                ball = ball with { Velocity = newVelocity, Position = newPosition };
            }

            Console.WriteLine($"?? Bounce Test Results:");
            Console.WriteLine($"   Total bounces detected: {bounceHeights.Count}");

            Assert.IsTrue(bounceHeights.Count >= 3,
                    $"Should have at least 3 bounces to verify energy loss, got {bounceHeights.Count}");

            for (int i = 1; i < Math.Min(5, bounceHeights.Count); i++)
            {
                double previousPeak = bounceHeights[i - 1];
                double currentPeak = bounceHeights[i];
                double heightDrop = currentPeak - previousPeak;

                Console.WriteLine($"   Bounce {i}: Peak at Y={currentPeak:F1} (dropped {heightDrop:F1}px lower)");

                Assert.IsTrue(currentPeak > previousPeak,
               $"Bounce {i} should peak at higher Y (lower height) than bounce {i - 1}. " +
              $"Previous peak: {previousPeak:F1}, Current peak: {currentPeak:F1}");
            }

            Assert.IsTrue(ball.Position.Y > 400,
                $"Ball should settle significantly below start (100px). Final Y: {ball.Position.Y:F1}");

            Assert.IsTrue(Math.Abs(ball.Velocity.Y) < 10,
     $"Ball should be moving slower after energy loss. Final velocity: {Math.Abs(ball.Velocity.Y):F1}");

            Console.WriteLine($"   Final position: Y={ball.Position.Y:F1}px (started at Y=100)");
            Console.WriteLine($"   Final velocity: {Math.Abs(ball.Velocity.Y):F1} px/frame (started at 0)");
            Console.WriteLine($"   ? Energy loss verified through increasing bounce peak Y values");
        }

        [TestMethod]
        public void Ball_ElasticityCoefficient_DeterminesEnergyLoss()
        {
            double canvasHeight = 800;
            var ball = new Ball(new Vector2D(400, 790), new Vector2D(0, 10), 10, "#FF0000");
            double elasticity = 0.8;
            double initialSpeed = 10.0;

            var newPosition = ball.Position + ball.Velocity;

            if (newPosition.Y + ball.Radius > canvasHeight)
            {
                newPosition = new Vector2D(newPosition.X, canvasHeight - ball.Radius);
                var newVelocity = new Vector2D(ball.Velocity.X, -ball.Velocity.Y * elasticity);
                ball = ball with { Position = newPosition, Velocity = newVelocity };
            }

            double expectedVelocity = initialSpeed * elasticity;
            Assert.AreEqual(expectedVelocity, Math.Abs(ball.Velocity.Y), 0.001,
                  $"After bounce with elasticity {elasticity}, velocity should be {expectedVelocity}");

            double initialKineticEnergy = 0.5 * initialSpeed * initialSpeed;
            double finalKineticEnergy = 0.5 * Math.Abs(ball.Velocity.Y) * Math.Abs(ball.Velocity.Y);
            double energyRetentionRatio = finalKineticEnergy / initialKineticEnergy;
            double expectedEnergyRetention = elasticity * elasticity;

            Console.WriteLine($"? Energy Loss Test:");
            Console.WriteLine($"   Initial velocity: {initialSpeed} px/frame");
            Console.WriteLine($"   Final velocity: {Math.Abs(ball.Velocity.Y):F2} px/frame");
            Console.WriteLine($"   Energy retention: {energyRetentionRatio:P0} (expected {expectedEnergyRetention:P0})");
            Console.WriteLine($"   Energy lost: {(1 - energyRetentionRatio):P0}");

            Assert.AreEqual(expectedEnergyRetention, energyRetentionRatio, 0.001,
               "Energy retention should equal elasticity squared (E ? v�)");
        }

        #endregion

        #region Random Color Generation Tests

        [TestMethod]
        public void RandomColor_GeneratesValidHexColor()
        {
            var colors = new[] { "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FF00FF", "#00FFFF" };
            var color = colors[_random.Next(colors.Length)];

            Assert.IsTrue(color.StartsWith("#"), "Color should start with #");
            Assert.AreEqual(7, color.Length, "Hex color should be 7 characters");
        }

        [TestMethod]
        public void RandomPosition_WithinBounds()
        {
            double canvasWidth = 1200;
            double canvasHeight = 800;
            double ballSize = 10;

            double x = _random.NextDouble() * (canvasWidth - ballSize * 2) + ballSize;
            double y = _random.NextDouble() * (canvasHeight / 2);

            Assert.IsTrue(x >= ballSize && x <= canvasWidth - ballSize, "X should be within bounds");
            Assert.IsTrue(y >= 0 && y <= canvasHeight / 2, "Y should be in top half");
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void Ball_ZeroVelocity_StaysStill()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(0, 0), 10, "#FF0000");
            var initialPosition = ball.Position;

            var newPosition = ball.Position + ball.Velocity;

            Assert.AreEqual(initialPosition.X, newPosition.X);
            Assert.AreEqual(initialPosition.Y, newPosition.Y);
        }

        [TestMethod]
        public void Ball_ZeroGravity_NoAcceleration()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(5, 5), 10, "#FF0000");
            double gravity = 0;
            double timeScale = 1.0;

            var gravityForce = new Vector2D(0, gravity * timeScale);
            var newVelocity = ball.Velocity + gravityForce;

            Assert.AreEqual(ball.Velocity.X, newVelocity.X);
            Assert.AreEqual(ball.Velocity.Y, newVelocity.Y, "Velocity should not change with zero gravity");
        }

        [TestMethod]
        public void Ball_FullAirResistance_StopsImmediately()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(10, 5), 10, "#FF0000");
            double airResistance = 0.0;

            var newVelocity = ball.Velocity * airResistance;

            Assert.AreEqual(0, newVelocity.X);
            Assert.AreEqual(0, newVelocity.Y, "Ball should stop with full air resistance");
        }

        [TestMethod]
        public void Ball_NoAirResistance_MaintainsVelocity()
        {
            var ball = new Ball(new Vector2D(100, 100), new Vector2D(10, 5), 10, "#FF0000");
            double airResistance = 1.0;

            var newVelocity = ball.Velocity * airResistance;

            Assert.AreEqual(10, newVelocity.X);
            Assert.AreEqual(5, newVelocity.Y, "Velocity should be maintained with no air resistance");
        }

        #endregion
    }
}
