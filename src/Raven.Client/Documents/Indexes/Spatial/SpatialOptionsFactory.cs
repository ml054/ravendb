using System;

namespace Raven.Client.Documents.Indexes.Spatial
{
    public class SpatialOptionsFactory
    {
        public GeographySpatialOptionsFactory Geography => new GeographySpatialOptionsFactory();

        public CartesianSpatialOptionsFactory Cartesian => new CartesianSpatialOptionsFactory();
    }

    public class GeographySpatialOptionsFactory
    {
        /// <summary>
        /// Defines a Geohash Prefix Tree index using a default Max Tree Level <see cref="SpatialOptions.DefaultGeohashLevel" />
        /// </summary>
        /// <returns></returns>
        public SpatialOptions Default(SpatialUnits circleRadiusUnits = SpatialUnits.Kilometers)
        {
            return GeohashPrefixTreeIndex(0, circleRadiusUnits);
        }

        public SpatialOptions BoundingBoxIndex(SpatialUnits circleRadiusUnits = SpatialUnits.Kilometers)
        {
            return new SpatialOptions
            {
                Type = SpatialFieldType.Geography,
                Strategy = SpatialSearchStrategy.BoundingBox,
                Units = circleRadiusUnits
            };
        }

        public SpatialOptions GeohashPrefixTreeIndex(int maxTreeLevel, SpatialUnits circleRadiusUnits = SpatialUnits.Kilometers)
        {
            if (maxTreeLevel == 0)
                maxTreeLevel = SpatialOptions.DefaultGeohashLevel;

            return new SpatialOptions
            {
                Type = SpatialFieldType.Geography,
                MaxTreeLevel = maxTreeLevel,
                Strategy = SpatialSearchStrategy.GeohashPrefixTree,
                Units = circleRadiusUnits
            };
        }

        public SpatialOptions QuadPrefixTreeIndex(int maxTreeLevel, SpatialUnits circleRadiusUnits = SpatialUnits.Kilometers)
        {
            if (maxTreeLevel == 0)
                maxTreeLevel = SpatialOptions.DefaultQuadTreeLevel;

            return new SpatialOptions
            {
                Type = SpatialFieldType.Geography,
                MaxTreeLevel = maxTreeLevel,
                Strategy = SpatialSearchStrategy.QuadPrefixTree,
                Units = circleRadiusUnits
            };
        }
    }

    public class CartesianSpatialOptionsFactory
    {
        public SpatialOptions BoundingBoxIndex()
        {
            return new SpatialOptions
            {
                Type = SpatialFieldType.Cartesian,
                Strategy = SpatialSearchStrategy.BoundingBox
            };
        }

        public SpatialOptions QuadPrefixTreeIndex(int maxTreeLevel, SpatialBounds bounds)
        {
            if (maxTreeLevel == 0)
                throw new ArgumentOutOfRangeException(nameof(maxTreeLevel));

            return new SpatialOptions
            {
                Type = SpatialFieldType.Cartesian,
                MaxTreeLevel = maxTreeLevel,
                Strategy = SpatialSearchStrategy.QuadPrefixTree,
                MinX = bounds.MinX,
                MinY = bounds.MinY,
                MaxX = bounds.MaxX,
                MaxY = bounds.MaxY
            };
        }
    }

    public class SpatialBounds
    {
        public double MinX { get; }
        public double MaxX { get; }
        public double MinY { get; }
        public double MaxY { get; }

        public SpatialBounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        protected bool Equals(SpatialBounds other)
        {
            return MinX.Equals(other.MinX) && MaxX.Equals(other.MaxX) && MinY.Equals(other.MinY) && MaxY.Equals(other.MaxY);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SpatialBounds)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = MinX.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxX.GetHashCode();
                hashCode = (hashCode * 397) ^ MinY.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxY.GetHashCode();
                return hashCode;
            }
        }
    }
}
