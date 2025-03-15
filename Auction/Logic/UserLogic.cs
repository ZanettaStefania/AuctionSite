using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TAP21_22_AuctionSite.Interface;

namespace Zanetta.Logic
{
    public class UserLogic : IUser
    {
        public UserLogic(string username, ISite site)
        {
            Username = username;
            SiteFrom = site;
        }
        public string Username { get; }
        public ISite SiteFrom { get; }
        private bool Deleted { get; set; }

        public IEnumerable<IAuction> WonAuctions()
        {
            IsDeleted();

            var temp = SiteFrom.ToyGetAuctions(false)
                .Where(au => au.EndsOn <= ((UserLogic) au.Seller).SiteFrom.Now() && au.CurrentWinner()?.Username == Username);
            return temp;

        }

        public void Delete()
        {
            IsDeleted();
            IEnumerable<IAuction> ListNotEndedAuctions = SiteFrom.ToyGetAuctions(true)
                .Where(au => (au.Seller.Username == Username || au.CurrentWinner()?.Username == Username)).AsEnumerable();
            
            if (ListNotEndedAuctions.Any())
            {
                throw new AuctionSiteInvalidOperationException("Found not ended Auction.");
            }
            else
            {
                SiteLogic s = (SiteLogic) SiteFrom;
                IEnumerable<IAuction> auctions = SiteFrom.ToyGetAuctions(false)
                    .Where(au => au.Seller.Username == Username || au.CurrentWinner()?.Username == Username);

                foreach (var auct in auctions)
                {
                    if (auct.Seller.Username == Username)
                    {
                        auct.Delete();
                    }
                    else
                    {
                        using (var c = new TapDbContext(s.connectionString))
                        {
                            try
                            {
                                var EntityAuct = c.Auction.SingleOrDefault(au => au.Id == auct.Id);
                                EntityAuct.CurrentWinner = null;
                                EntityAuct.CurrentWinnerId = null;
                                c.SaveChanges();
                            }
                            catch (Exception e)
                            {
                                throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                            }
                        }
                    }
                }

                var ses = SiteFrom.ToyGetSessions().SingleOrDefault(s => s.User.Username == this.Username);
                ses?.Logout();

                using (var c = new TapDbContext(s.connectionString))
                {
                    try
                    {
                        var EntityUser = c.Users.Include(u => u.Site).SingleOrDefault(u => u.Username == Username && u.Site.Name==SiteFrom.Name);
                        c.Users.Remove(EntityUser);
                        c.SaveChanges();
                        Deleted = true;
                    }
                    catch (Exception e)
                    {
                        throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                    }
                }

            }

        }

        public override bool Equals(object? o)
        {
            return Equals(o as UserLogic);
        }

        public bool Equals(UserLogic user)
        {
            return this.Username == user.Username && ((SiteLogic) SiteFrom).Name == ((SiteLogic) user.SiteFrom).Name;
        }

        public override int GetHashCode()
        {
            return Username.GetHashCode();
        }

        //Funzioni Ausiliarie

        private void IsDeleted()
        {
            if (Deleted)
            {
                throw new AuctionSiteInvalidOperationException($"{nameof(Username)} was deleted.");
            }
        }
    }
}
