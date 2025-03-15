using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace Zanetta.Logic
{
    public class SiteLogic : ISite
    {
        public SiteLogic(string connectionString, string name, int timezone, int sessionExpirationInSeconds,
            double minimumBidIncrement, IAlarmClockFactory Clock)
        {
            this.connectionString = connectionString;
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            myClock = Clock;
            IAlarm alarm = myClock.InstantiateAlarmClock(timezone)
                .InstantiateAlarm(5 * 60 * 1000); /* 5*60*1000 = 300000 = 5 minutes */
            alarm.RingingEvent += GetRidOfSessions;
            deleted = false;
        }

        public IAlarmClockFactory myClock;
        public string connectionString { get; }
        public string Name { get; }
        public int Timezone { get; }
        public int SessionExpirationInSeconds { get; }
        public double MinimumBidIncrement { get; }
        private int sizeSalt = 8;
        private bool deleted;

        private class HashSalt
        {
            public string Hash { get; set; }
            public string Salt { get; set; }
        }

        private string HashPassword(string password)
        {
            var saltBytes = new byte[sizeSalt];
            var provider = new RNGCryptoServiceProvider();
            provider.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);

            var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, saltBytes, 10000);
            var hashPassword = Convert.ToBase64String(rfc2898DeriveBytes.GetBytes(10));

            HashSalt hashSalt = new HashSalt {Hash = hashPassword, Salt = salt};
            return $"{hashPassword}::{salt}";
        }

        private bool VerifyPassword(string hashpass, string password)
        {
            String[] separator = {"::"};
            String[] str = hashpass.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            var salt = Convert.FromBase64String(str[1]);
            var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, 10000);
            return (Convert.ToBase64String(rfc2898DeriveBytes.GetBytes(10)) == str[0]);
        }

        public IEnumerable<IUser> ToyGetUsers()
        {
            IsDeleted();
            int siteId = 0;
            using (var c = new TapDbContext(connectionString))
            {
                siteId = c.Sites.Single(s => s.Name == this.Name).SiteId;
            }

            using (var c = new TapDbContext(connectionString))
            {
                var foundUsers = c.Users.Where(u => u.SiteId == siteId).AsEnumerable();
                return CreateListUser(foundUsers);
            }
        }

        public IEnumerable<ISession> ToyGetSessions()
        {
            IsDeleted();
            List<SessionLogic> sessions = new List<SessionLogic>();

            using (var c = new TapDbContext(connectionString))
            {
                IEnumerable<Session> sessionSites =
                    c.Sessions.Include(ses =>
                        ses.Site).Include(ses => ses.UserFrom).Where(ses => ses.Site.Name == this.Name).AsEnumerable();

                foreach (var sessionInfo in sessionSites)
                {
                    if (sessionInfo.ValidUntil > Now())
                        sessions.Add(new SessionLogic(sessionInfo.Id, sessionInfo.ValidUntil,
                            new UserLogic(sessionInfo.UserFrom.Username, this)));
                }
            }

            return sessions;
        }

        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded)
        {
            IsDeleted();
            List<IAuction> auctionList = new List<IAuction>();
            using (var c = new TapDbContext(connectionString))
            {
                IEnumerable<Auction> auctions = c.Auction.Include(a => a.Seller).ThenInclude(u => u.Site)
                    .Where(u => u.Seller.Site.Name == this.Name).AsEnumerable();

                foreach (var auct in auctions)
                {
                    auctionList.Add(new AuctionLogic(auct.Id, new UserLogic(auct.Seller.Username, this),
                        auct.Description, auct.EndsOn));
                }
            }

            if (onlyNotEnded)
            {
                return auctionList.Where(a => a.EndsOn > Now());
            }

            return auctionList;
        }

        public ISession? Login(string username, string password)
        {
            IsUsernamePasswordValid(username, password);

            int siteId = 0;
            int userId = 0;
            IUser userFound;

            IsDeleted();

            //cerco se il sito esiste nel db
            using (var c = new TapDbContext(connectionString))
            {
                siteId = c.Sites.Single(s => s.Name == this.Name).SiteId;
            }

            //cerco utente se esiste nel db
            using (var c = new TapDbContext(connectionString))
            {
                var userExist = c.Users.FirstOrDefault(u => u.Username == username && u.SiteId == siteId);
                if (userExist != null)
                {
                    String pass = userExist.Password;
                    if (!VerifyPassword(pass, password))
                        return null;
                    userFound = new UserLogic(userExist.Username, this);
                    userId = userExist.UserId;
                }
                else
                    return null;
            }

            //cerco sessione aperta nel db, se esiste ritorno la sessione trovata
            using (var c = new TapDbContext(connectionString))
            {
                var oldSession = c.Sessions.FirstOrDefault(s => s.UserId == userId);
                if (oldSession != null)
                {
                    try
                    {
                        oldSession.ValidUntil = Now().AddSeconds(SessionExpirationInSeconds);
                        c.SaveChanges();
                        return new SessionLogic($"{userId}::{siteId}", oldSession.ValidUntil, userFound);
                    }
                    catch (Exception e)
                    {
                        throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                    }
                }
            }

            //altrimenti creo nuova sessione
            using (var c = new TapDbContext(connectionString))
            {
                DateTime validTime = Now().AddSeconds(SessionExpirationInSeconds);
                var newSession = new Session($"{userId}::{siteId}", userId, validTime, siteId);
                try
                {
                    c.Sessions.Add(newSession);
                    c.SaveChanges();
                    return new SessionLogic($"{userId}::{siteId}", validTime, userFound);
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unreachable DB", e);
                }
            }
        }

        public void CreateUser(string username, string password)
        {
            IsUserValid(username, password);
            IsDeleted();
            try
            {
                int siteId;
                using (var c = new TapDbContext(connectionString))
                {
                    siteId = c.Sites.Single(s => s.Name == this.Name).SiteId;
                }

                using (var c = new TapDbContext(connectionString))
                {
                    var userExist = c.Users.FirstOrDefault(u => u.Username == username && u.SiteId == siteId);
                    if (userExist != null)
                    {
                        throw new AuctionSiteNameAlreadyInUseException(
                            $"{nameof(username)} already exist in this site.");
                    }
                }

                using (var c = new TapDbContext(connectionString))
                {
                    try
                    {
                        var newUser = new User(username, HashPassword(password), siteId);
                        c.Users.Add(newUser);
                        c.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                    }
                }
            }
            catch (SqlException e)
            {
                throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
            }
        }

        public void Delete()
        {
            foreach (var auct in ToyGetAuctions(true))
            {
                auct.Delete();
            }

            foreach (var user in ToyGetUsers())
            {
                user.Delete();
            }

            using (var c = new TapDbContext(connectionString))
            {
                try
                {
                    var entitySites = c.Sites.SingleOrDefault(s => s.Name == this.Name);
                    c.Sites.Remove(entitySites);
                    c.SaveChanges();
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
            }

            deleted = true;
        }

        public DateTime Now()
        {
            return myClock.InstantiateAlarmClock(Timezone).Now;
        }

        public void GetRidOfSessions()
        {
            if (deleted)
                return;

            List<SessionLogic> sessions = new List<SessionLogic>();
            using (var c = new TapDbContext(connectionString))
            {
                IEnumerable<Session> sessionSites =
                    c.Sessions.Include(ses =>
                        ses.Site).Include(ses => ses.UserFrom).Where(ses => ses.Site.Name == this.Name).AsEnumerable();

                foreach (var sessionInfo in sessionSites)
                {
                    if (sessionInfo.ValidUntil > Now())
                        sessions.Add(new SessionLogic(sessionInfo.Id, sessionInfo.ValidUntil,
                            new UserLogic(sessionInfo.UserFrom.Username, this)));
                }
            }

            if (!sessions.Any())
                return;

            foreach (var Session in sessions)
            {
                if (Session.ValidUntil <= Now())
                {
                    Session.Logout();
                }
            }

            return;
        }

        public override bool Equals(object? o)
        {
            return Equals(o as ISite);
        }

        public bool Equals(ISite site)
        {
            return this.Name == site.Name;
        }


        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        //Funzioni Ausiliarie

        private IEnumerable<IUser> CreateListUser(IEnumerable<User> userList)
        {
            List<UserLogic> createdUsers = new List<UserLogic>();
            foreach (var u in userList)
            {
                createdUsers.Add(new UserLogic(u.Username, this));
            }

            return createdUsers;
        }

        private void IsUserValid(string username, string password)
        {
            IsUsernamePasswordValid(username, password);
            if ((password.Length < DomainConstraints.MinUserPassword))
            {
                throw new AuctionSiteArgumentException(
                    $"{nameof(password)} must be longer than {DomainConstraints.MinUserPassword} characters.");
            }

            if ((username.Length < DomainConstraints.MinUserName) || (username.Length > DomainConstraints.MaxUserName))
            {
                throw new AuctionSiteArgumentException(
                    $"{nameof(username)} must be strictly smaller than {DomainConstraints.MaxUserName} characters but greater than {DomainConstraints.MinUserName} character.");
            }
        }

        private void IsUsernamePasswordValid(string username, string password)
        {
            if (username == null || password == null)
            {
                throw new AuctionSiteArgumentNullException(
                    $"{nameof(username)} and {nameof(password)} must be not null.");
            }
        }

        private void IsDeleted()
        {
            if (deleted)
            {
                throw new AuctionSiteInvalidOperationException($"{nameof(Name)} is already deleted.");
            }
        }
    }
}