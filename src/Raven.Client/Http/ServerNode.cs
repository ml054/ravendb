﻿using System;
using Raven.Client.Util.Metrics;

namespace Raven.Client.Http
{
    public class ServerNode
    {
        [Flags]
        public enum Role
        {
            None = 0,
            Promotable = 1,
            Member = 2,
            Rehab = 4
        }

        public string Url;
        public string Database;
        public string ClusterTag;
        public Role ServerRole;

        private readonly EWMA _ewma = new EWMA(EWMA.M1Alpha, 1, TimeUnit.Milliseconds);
        private const double SwitchBackRatio = 0.75;
        private bool _isRateSurpassed;

        public ServerNode()
        {
            for (var i = 0; i < 60; i++)
                UpdateRequestTime(0);
        }

        public void UpdateRequestTime(long requestTimeInMilliseconds)
        {
            _ewma.Update(requestTimeInMilliseconds);
            _ewma.Tick();
        }

        public bool IsRateSurpassed(double requestTimeSlaThresholdInMilliseconds)
        {
            var rate = Rate();

            if (_isRateSurpassed)
                return _isRateSurpassed = rate >= SwitchBackRatio * requestTimeSlaThresholdInMilliseconds;

            return _isRateSurpassed = rate >= requestTimeSlaThresholdInMilliseconds;
        }

        public double Rate()
        {
            return _ewma.Rate(TimeUnit.Milliseconds);
        }

        private bool Equals(ServerNode other)
        {
            return string.Equals(Url, other.Url) &&
                string.Equals(Database, other.Database);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ServerNode)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Url?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (Database?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        private const double MaxDecreasingRatio = 0.75;
        private const double MinDecreasingRatio = 0.25;

        public void DecreaseRate(long requestTimeInMilliseconds)
        {
            var rate = Rate();
            var maxRate = MaxDecreasingRatio * rate;
            var minRate = MinDecreasingRatio * rate;

            var decreasingRate = rate - requestTimeInMilliseconds;

            if (decreasingRate > maxRate)
                decreasingRate = maxRate;

            if (decreasingRate < minRate)
                decreasingRate = minRate;

            UpdateRequestTime((long)decreasingRate);
        }
    }
}
