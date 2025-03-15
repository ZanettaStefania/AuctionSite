using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;

namespace Zanetta.Logic
{
    public class SessionLogic : ISession
    {
        public SessionLogic(string id, DateTime validUntil, IUser user)
        {
            Id = id;
            ValidUntil = validUntil;
            User = user;
            SiteLogic siteFrom = (SiteLogic) (((UserLogic) user).SiteFrom);
            Clock = siteFrom.myClock.InstantiateAlarmClock(siteFrom.Timezone);
        }

        public string Id { get; }
        public DateTime ValidUntil { get; set; }
        public IUser User { get; }
        public IAlarmClock Clock { get; }

        public void Logout()
        {
            SiteLogic siteFrom = (SiteLogic) (((UserLogic) User).SiteFrom);
            var sessionFound = FindSession();

            if (sessionFound.ValidUntil < Clock.Now)
                throw new AuctionSiteInvalidOperationException("Session was expired.");

            this.ValidUntil = Clock.Now;

            using (var c = new TapDbContext(siteFrom.connectionString))
            {
                try
                {
                    Session s = c.Sessions.SingleOrDefault(ses => ses.Id == this.Id);
                    c.Users.Where(u => u.Username == User.Username).Load();
                    c.Sessions.Remove(s);
                    c.SaveChanges();
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
            }
        }

        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            IsAuctionValid(description, endsOn, startingPrice);
            SiteLogic siteFrom = (SiteLogic) (((UserLogic) User).SiteFrom);
            int sellerId;

            // Check the User
            using (var c = new TapDbContext(siteFrom.connectionString))
            {
                var userFound = c.Users.Include(u => u.Site).SingleOrDefault(u =>
                    u.Username == User.Username && u.Site.Name == ((UserLogic) User).SiteFrom.Name);
                if (userFound == null)
                    throw new AuctionSiteInexistentNameException("Site doesn't exist or was deleted.");
                else
                    sellerId = userFound.UserId;
            }

            // Add Auction 
            IAuction newAuction;
            using (var c = new TapDbContext(siteFrom.connectionString))
            {
                try
                {
                    var newAuctRow = c.Auction.Add(new Auction(sellerId, startingPrice, description, endsOn));
                    c.SaveChanges();
                    newAuction = new AuctionLogic(newAuctRow.Entity.Id, User, description, endsOn);
                    this.ValidUntil = Clock.Now.AddSeconds(siteFrom.SessionExpirationInSeconds);
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
            }

            // Update Session
            using (var c = new TapDbContext(siteFrom.connectionString))
            {
                var session = c.Sessions.SingleOrDefault(s => s.Id == this.Id);
                if (session != null)
                {
                    session.ValidUntil = ValidUntil;
                    c.SaveChanges();
                }
            }

            return newAuction;
        }

        public override bool Equals(object? o)
        {
            return Equals(o as SessionLogic);
        }

        public bool Equals(SessionLogic session)
        {
            return this.Id == session.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        //Funzioni Ausiliarie

        private void IsAuctionValid(string description, DateTime endsOn, double startingPrice)
        {
            if (this.ValidUntil <= Clock.Now)
                throw new AuctionSiteInvalidOperationException("Invalid session");

            if (description == null)
                throw new AuctionSiteArgumentNullException($"{nameof(description)} must be not null.");

            if (string.IsNullOrEmpty(description))
                throw new AuctionSiteArgumentException($"{nameof(description)} must be not empty.");

            if (startingPrice <= 0)
                throw new AuctionSiteArgumentOutOfRangeException(
                    $"{nameof(startingPrice)} must be positive and grater than 0.");

            if (endsOn <= Clock.Now)
                throw new AuctionSiteUnavailableTimeMachineException($"{endsOn} must not be already expired.");
        }

        private Session FindSession()
        {
            SiteLogic siteFrom = (SiteLogic) (((UserLogic) User).SiteFrom);
            using (var c = new TapDbContext(siteFrom.connectionString))
            {
                var sessionFound = c.Sessions.SingleOrDefault(ses => ses.Id == this.Id);
                if (sessionFound == null)
                    throw new AuctionSiteArgumentException("Session doesn't exist or was deleted.");
                return sessionFound;
            }
        }
    }
}