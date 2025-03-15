using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace Zanetta.Logic
{
    public class HostLogic : IHost
    {
        public String MyConnectionString;
        public IAlarmClockFactory MyAlarmClockFactory;

        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            using (var c = new TapDbContext(MyConnectionString))
            {
                try
                {
                    var siteInfo = c.Sites.AsEnumerable()
                        .Select(x => (x.Name, x.Timezone)).ToList();
                    return siteInfo;
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
            }
        }

        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds,
            double minimumBidIncrement)
        {
            IsSiteValid(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement);

            using (var c = new TapDbContext(MyConnectionString))
            {
                var found = c.Sites.SingleOrDefault(s => s.Name == name);
                if (found != null)
                {
                    throw new AuctionSiteNameAlreadyInUseException($"{nameof(name)} is already in use.");
                }
            }

            using (var c = new TapDbContext(MyConnectionString))
            {
                var newSite = new Site(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement);
                try
                {
                    c.Sites.Add(newSite);
                    c.SaveChanges();
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
                catch (Exception e)
                {
                    throw new AuctionSiteNameAlreadyInUseException(
                        $"{nameof(name)} is already in use, please choose another SiteLogic name.");
                }
            }
        }

        public ISite LoadSite(string name)
        {
            IsSiteNameValid(name);

            using (var c = new TapDbContext(MyConnectionString))
            {
                try
                {
                    var infoSite = c.Sites.Single(s => s.Name == name);
                    var s = new SiteLogic(MyConnectionString, infoSite.Name, infoSite.Timezone,
                        infoSite.SessionExpirationInSeconds,
                        infoSite.MinimumBidIncrement,
                        MyAlarmClockFactory);
                    return s;
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
                catch (InvalidOperationException e)
                {
                    throw new AuctionSiteInexistentNameException(
                        $"{nameof(name)} does not exist, therefore can't be loaded.");
                }
            }
        }

        //Funzioni Ausiliarie
        private void IsSiteValid(string name, int timezone, int sessionExpirationTimeInSeconds,
            double minimumBidIncrement)
        {
            IsSiteNameValid(name);
            if ((timezone < DomainConstraints.MinTimeZone) || (timezone > DomainConstraints.MaxTimeZone))
                throw new AuctionSiteArgumentOutOfRangeException(
                    $"{nameof(timezone)} must be strictly smaller than {DomainConstraints.MaxTimeZone} characters but greater than {DomainConstraints.MinTimeZone} character.");

            if (sessionExpirationTimeInSeconds < 0)
                throw new AuctionSiteArgumentOutOfRangeException(
                    $"{nameof(sessionExpirationTimeInSeconds)} must be a positive number.");

            if (minimumBidIncrement < 0)
                throw new AuctionSiteArgumentOutOfRangeException(
                    $"{nameof(minimumBidIncrement)} must be a positive number.");
        }

        private void IsSiteNameValid(string name)
        {
            if (name == null)
                throw new AuctionSiteArgumentNullException($"{nameof(name)} is null or empty.");

            if ((name.Length < DomainConstraints.MinSiteName) || (name.Length > DomainConstraints.MaxSiteName))
                throw new AuctionSiteArgumentException(
                    $"{nameof(name)} must be strictly smaller than 128 characters but larger than 1 character.");
        }
    }
}