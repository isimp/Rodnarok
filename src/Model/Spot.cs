using System;

namespace Rodnarok
{
    /// <summary>A place in the world, x and z across the ground and y up, as the game counts them, without Unity's types so the model builds anywhere.</summary>
    internal readonly struct Spot : IEquatable<Spot>
    {
        public Spot(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        /// <summary>The straight distance to another place, through all three axes.</summary>
        public float To(Spot other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>The distance to another place across the ground, height left out.</summary>
        public float Across(Spot other)
        {
            var dx = X - other.X;
            var dz = Z - other.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Equals(Spot other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Spot spot && Equals(spot);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public static bool operator ==(Spot left, Spot right) => left.Equals(right);
        public static bool operator !=(Spot left, Spot right) => !left.Equals(right);
        public override string ToString() => FormattableString.Invariant($"({X}, {Y}, {Z})");
    }
}
