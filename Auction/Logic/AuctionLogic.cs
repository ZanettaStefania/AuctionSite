using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TAP21_22_AuctionSite.Interface;
using Zanetta;
using Zanetta.Logic;

namespace Zanetta.Logic
{
    public class AuctionLogic : IAuction
    {
        public AuctionLogic(int id, IUser seller, string description, DateTime endsOn)
        {
            Id = id;
            Seller = seller;
            Description = description;
            EndsOn = endsOn;
        }

        public int Id { get; }
        public IUser Seller { get; }
        public string Description { get; }
        public DateTime EndsOn { get; }
        private bool deleted;

        public IUser? CurrentWinner()
        {
            SiteLogic connect = (SiteLogic) ((UserLogic) Seller).SiteFrom;
            using (var c = new TapDbContext(connect.connectionString))
            {
                var found = c.Auction.Include(au => au.CurrentWinner).SingleOrDefault(au => au.Id == Id);
                if (found.CurrentWinner == null)
                    return null;
                else
                {
                    return new UserLogic(found.CurrentWinner.Username, ((UserLogic) Seller).SiteFrom);
                }
            }
        }

        public double CurrentPrice()
        {
            SiteLogic connect = (SiteLogic) ((UserLogic) Seller).SiteFrom;
            using (var c = new TapDbContext(connect.connectionString))
            {
                var found = c.Auction.SingleOrDefault(au => au.Id == Id);
                if (found == null)
                    throw new AuctionSiteInvalidOperationException("Auction not found.");
                else
                {
                    return (found.CurrentPrice);
                }
            }
        }

        public void Delete()
        {
            SiteLogic connect = (SiteLogic) ((UserLogic) Seller).SiteFrom;
            Auction auction;

            using (var c = new TapDbContext(connect.connectionString))
            {
                auction = c.Auction.SingleOrDefault(au => au.Id == this.Id);
                if (auction == null)
                {
                    throw new AuctionSiteInvalidOperationException("Auction not found.");
                }
            }

            using (var c = new TapDbContext(connect.connectionString))
            {
                try
                {
                    c.Auction.Remove(auction);
                    c.SaveChanges();
                    deleted = true;
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
            }
        }

        public bool Bid(ISession session, double offer)
        {
            IsBidValid(session, offer);


            double CMO = CurrentMaximumOffer();
            int bidderId = this.BidderId(session);

            if (bidderId == -1)
                throw new AuctionSiteInvalidOperationException("User doesn't exist or was deleted.");

            SiteLogic site = (SiteLogic) ((UserLogic) Seller).SiteFrom;

            // If there isn't a CurrentWinner, update HighestBid, CurrentWinnerId and update Session
            if (CurrentWinner() == null)
            {
                if (offer < CurrentPrice())
                    return false;


                using (var c = new TapDbContext(site.connectionString))
                {
                    try
                    {
                        var auction = c.Auction.SingleOrDefault(au => au.Id == this.Id);
                        if (auction != null)
                        {
                            auction.CurrentWinnerId = bidderId;
                            auction.HighestBid = offer;
                            c.SaveChanges();
                        }
                    }
                    catch (Exception e)
                    {
                        throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                    }
                }

                UpdateSession(session);

                return true;
            }

            // If Bidder is the CurrentWinner, update HighestBid if higher and update Session
            if (((UserLogic) session.User).Equals((UserLogic) CurrentWinner()))
            {
                if (CMO >= offer + site.MinimumBidIncrement)
                {
                    return false;
                }
                else
                {
                    using (var c = new TapDbContext(site.connectionString))
                    {
                        try
                        {
                            var auction = c.Auction.SingleOrDefault(au => au.Id == this.Id);
                            if (auction != null)
                            {
                                auction.HighestBid = offer;
                                c.SaveChanges();
                            }
                        }
                        catch (Exception e)
                        {
                            throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                        }
                    }

                    UpdateSession(session);

                    return true;
                }
            }
            else
            {
                // If Bidder is not the CurrentWinner
                if (offer < CurrentPrice() + site.MinimumBidIncrement)
                    return false;

                if (offer > CMO)
                {
                    // If offer is higher than CMO, update CurrentWinner, CurrentPrice, HighestBid and Session
                    using (var c = new TapDbContext(site.connectionString))
                    {
                        try
                        {
                            var auction = c.Auction.SingleOrDefault(au => au.Id == this.Id);
                            if (auction != null)
                            {
                                auction.CurrentWinnerId = bidderId;
                                auction.CurrentPrice = Math.Min(offer, auction.HighestBid + site.MinimumBidIncrement);
                                auction.HighestBid = offer;
                                c.SaveChanges();
                            }
                        }
                        catch (Exception e)
                        {
                            throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                        }
                    }

                    UpdateSession(session);

                    return true;
                }
                else
                {
                    // If offer isn't higher than CMO, update CurrentPrice and Session
                    using (var c = new TapDbContext(site.connectionString))
                    {
                        try
                        {
                            var auction = c.Auction.SingleOrDefault(au => au.Id == this.Id);
                            if (auction != null)
                            {
                                auction.CurrentPrice = Math.Min(auction.HighestBid, offer + site.MinimumBidIncrement);
                                c.SaveChanges();
                            }
                        }
                        catch (Exception e)
                        {
                            throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                        }
                    }

                    UpdateSession(session);

                    return true;
                }
            }
        }

        public override bool Equals(object? o)
        {
            return Equals(o as AuctionLogic);
        }

        public bool Equals(AuctionLogic auction)
        {
            return (this.Id == auction.Id &&
                    ((UserLogic) this.Seller).SiteFrom.Name == ((UserLogic) auction.Seller).SiteFrom.Name);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        //Funzioni Ausiliarie
        private void IsBidValid(ISession session, double offer)
        {
            if (deleted)
                throw new AuctionSiteInvalidOperationException("Auction was deleted");
            if (offer < 0)
                throw new AuctionSiteArgumentOutOfRangeException($"{offer} must be a positive number");
            if (session == null)
                throw new AuctionSiteArgumentNullException($"{nameof(session)} doesn't exist.");
            if (session.ValidUntil <= ((UserLogic) Seller).SiteFrom.Now())
                throw new AuctionSiteArgumentException($"{nameof(session)} is not valid.");
            if (Seller.Equals(session.User))
                throw new AuctionSiteArgumentException($"{nameof(Seller)} is not valid.");
            if (!((UserLogic) Seller).SiteFrom.Equals(((UserLogic) session.User).SiteFrom))
                throw new AuctionSiteArgumentException("User from a different site.");
            if (EndsOn <= ((UserLogic) Seller).SiteFrom.Now())
                throw new AuctionSiteInvalidOperationException("Auction has ended");
        }

        private double CurrentMaximumOffer()
        {
            SiteLogic site = (SiteLogic) ((UserLogic) Seller).SiteFrom;
            using (var c = new TapDbContext(site.connectionString))
            {
                var CMO = c.Auction.Single(au => au.Id == this.Id).HighestBid;
                return CMO;
            }
        }

        private int BidderId(ISession session)
        {
            SiteLogic site = (SiteLogic) ((UserLogic) Seller).SiteFrom;
            using (var c = new TapDbContext(site.connectionString))
            {
                var bidder = c.Users.SingleOrDefault(us =>
                    us.Username == session.User.Username && us.Site.Name == ((UserLogic) session.User).SiteFrom.Name);
                if (bidder != null)
                    return bidder.UserId;
                return -1;
            }
        }

        private void UpdateSession(ISession session)
        {
            SiteLogic site = (SiteLogic) ((UserLogic) Seller).SiteFrom;
            using (var c = new TapDbContext(site.connectionString))
            {
                try
                {
                    var ses = c.Sessions.SingleOrDefault(ses => ses.Id == session.Id);
                    if (ses != null)
                    {
                        ses.ValidUntil = site.Now().AddSeconds(site.SessionExpirationInSeconds);
                        ((SessionLogic) session).ValidUntil = ses.ValidUntil;
                        c.SaveChanges();
                    }
                }
                catch (Exception e)
                {
                    throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
                }
            }
        }
    }
}