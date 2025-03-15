using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TAP21_22_AuctionSite.Interface;
using Zanetta.Logic;

namespace TAP21_22.AuctionSite.Testing
{
    public class NewAuctionTests : AuctionSiteTests
    {
        /// <summary>
        ///     Initializes Site:
        ///     <list type="table">
        ///         <item>
        ///             <term>name</term>
        ///             <description>SiteName = "site for auction tests"</description>
        ///         </item>
        ///         <item>
        ///             <term>time zone</term>
        ///             <description>-2</description>
        ///         </item>
        ///         <item>
        ///             <term>expiration time</term>
        ///             <description>300 seconds</description>
        ///         </item>
        ///         <item>
        ///             <term>minimum bid increment</term>
        ///             <description>7</description>
        ///         </item>
        ///         <item>
        ///             <term>users</term>
        ///             <description>Seller, Bidder1, Bidder2</description>
        ///         </item>
        ///         <item>
        ///             <term>auctions</term>
        ///             <description>
        ///                 TheAuction ("Beautiful object to be desired by everybody",
        ///                 starting price 5, ends in 7 days)
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>sessions</term>
        ///             <description>SellerSession, Bidder1Session, Bidder2Session</description>
        ///         </item>
        ///     </list>
        /// </summary>
        [SetUp]
        public void SiteUsersAuctionInitialize()
        {
            const int timeZone = -2;
            TheHost.CreateSite(SiteName, timeZone, 300, 7);
            TheClock = (TestAlarmClock)TheAlarmClockFactory.InstantiateAlarmClock(timeZone);
            Site = TheHost.LoadSite(SiteName);
            Seller = CreateAndLogUser("seller", out SellerSession, Site);
            Bidder1 = CreateAndLogUser("bidder1", out Bidder1Session, Site);
            Bidder2 = CreateAndLogUser("bidder2", out Bidder2Session, Site);
            TheAuction = SellerSession.CreateAuction("Beautiful object to be desired by everybody", TheClock.Now.AddDays(7), 5);
        }

        protected ISite Site = null!;

        protected IUser Seller = null!;
        protected ISession SellerSession = null!;

        protected IUser Bidder1 = null!;
        protected ISession Bidder1Session = null!;

        protected IUser Bidder2 = null!;
        protected ISession Bidder2Session = null!;

        protected IAuction TheAuction = null!;

        protected const string SiteName = "site for auction tests";

        protected IUser CreateAndLogUser(string username, out ISession session, ISite site)
        {
            site.CreateUser(username, username);
            var user = site.ToyGetUsers().SingleOrDefault(u => u.Username == username);
            if (null == user)
                Assert.Inconclusive($"user {username} has not been created");
            var login = site.Login(username, username);
            if (null == login)
                Assert.Inconclusive($"user {username} should log in with password {username}");
            session = login!;
            return user!;
        }


        /// <summary>
        ///     Verify that the CurrentWinner is empty after deleting the User that was winning
        /// </summary>
        /*[Test]
        public void CurrentWinner_DeletedUser()
        {
            TheAuction.Bid(Bidder1Session, 10);
            TheClock!.Now = TheAuction.EndsOn.AddDays(1);
            ((SessionLogic) Bidder1Session).ValidUntil = TheClock.Now.AddMinutes(20);
            Bidder1.Delete();
            var currentWinner = TheAuction.CurrentWinner();
            Assert.That(currentWinner, Is.Null);
        }
        */

        /// <summary>
        ///     Verify that user cannot bid on ended auctions
        /// </summary>
        [Test]
        public void Bid_OnAlreadyEnded_Auction() 
         {
             TheClock!.Now = TheAuction.EndsOn.AddDays(1);
             ((SessionLogic) Bidder1Session).ValidUntil = TheClock.Now.AddSeconds(Site.SessionExpirationInSeconds);
             Assert.That( () => TheAuction.Bid(Bidder1Session, 20), Throws.TypeOf<AuctionSiteInvalidOperationException>());
        }

        /// <summary>
        ///     Verify that Seller cannot bid on his auctions
        /// </summary>
        [Test]
        public void Bid_Seller_Throw()
        {
            Assert.That( () => TheAuction.Bid(SellerSession, 30), Throws.TypeOf<AuctionSiteArgumentException>());
        }

        /// <summary>
        ///     Verify that Bidder in not from another Site
        /// </summary>
        [Test]
        public void Bid_FromDifferentSites_Throws()
        {
            TheHost.CreateSite("OtherSite", 5, 300, 7);
            Site = TheHost.LoadSite("OtherSite");
            ISession Bidder3Session;
            IUser Bidder3 = CreateAndLogUser("Bidder3", out Bidder3Session, Site);
            Assert.That(() => TheAuction.Bid(Bidder3Session, 50), Throws.TypeOf<AuctionSiteArgumentException>());
        }

        /// <summary>
        /// Verify that a call to CurrentPrice on a
        /// deleted auction throws InvalidOperationException
        /// </summary>
        [Test]
        public void CurrentPrice_OnDeletedObject_Throws()
        {
            TheAuction.Delete();
            Assert.That(() => TheAuction.CurrentPrice(), Throws.TypeOf<AuctionSiteInvalidOperationException>());
        }

    }
}
