using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using TAP21_22_AuctionSite.Interface;
using Zanetta.Logic;


namespace TAP21_22.AuctionSite.Testing
{
    public class NewSiteTests : AuctionSiteTests
    {
        /// <summary>
        ///     Initializes Site:
        ///     <list type="table">
        ///         <item>
        ///             <term>name</term>
        ///             <description>working site</description>
        ///         </item>
        ///         <item>
        ///             <term>time zone</term>
        ///             <description>5</description>
        ///         </item>
        ///         <item>
        ///             <term>expiration time</term>
        ///             <description>3600 seconds</description>
        ///         </item>
        ///         <item>
        ///             <term>minimum bid increment</term>
        ///             <description>3.5</description>
        ///         </item>
        ///         <item>
        ///             <term>users</term>
        ///             <description>empty list</description>
        ///         </item>
        ///         <item>
        ///             <term>auctions</term>
        ///             <description>empty list</description>
        ///         </item>
        ///         <item>
        ///             <term>sessions</term>
        ///             <description>empty list</description>
        ///         </item>
        ///     </list>
        /// </summary>
        [SetUp]
        public void SiteInitialize()
        {
            const string siteName = "working site";
            const int timeZone = 5;
            TheHost.CreateSite(siteName, timeZone, 3600, 3.5);
            Site = TheHost.LoadSite(siteName);
            TheClock = (TestAlarmClock)TheAlarmClockFactory.InstantiateAlarmClock(timeZone);
        }

        protected ISite Site = null!;

        private IEnumerable<IAuction> AddAuctions(DateTime endsOn, int auctionNumber)
        {
            Debug.Assert(auctionNumber > 0);
            var username = "pinco" + DateTime.Now.Ticks;
            Site.CreateUser(username, "pippo.123");
            var sellerSession = Site.Login(username, "pippo.123")!;
            var result = new List<IAuction>();
            for (var i = 0; i < auctionNumber; i++)
                result.Add(sellerSession.CreateAuction($"Auction {i} of {auctionNumber} ending on {endsOn}", endsOn, 7.7 * i + 11));
            return result;
        }

        /// <summary>
        ///     Verify that after creating a new user
        ///     we can't login  with their credentials
        ///     on a different site 
        /// </summary>
        [Test]
        public void Login_DiffrentSite()
        {
            ISite Site2 = CreateAndLoadEmptySite(-5, "different site", 360, 7, out TheClock);
            Site.CreateUser("UserName", "PassWord.12345");
            Assert.That( () => Site2.Login("UserName", "PassWord.12345"), Is.Null);
        }

        /// <summary>
        ///     Verify that site with the same name are equals
        /// </summary>
        [Test]
        public void Site_AreEquals()
        {
            SiteLogic Site2 = new SiteLogic("connection_string", "working site", 1, 2, 20, TheAlarmClockFactory);
            Assert.AreEqual(Site2, Site);
        }

        /// <summary>
        ///     Verify that a call to GetRidOfSessions
        ///     doesn't delete the Sessions that are still valid
        /// </summary>
        [TestCase(3, 2)]
        [TestCase(1, 1)]
        [TestCase(0, 1)]
        [TestCase(3, 0)]
        public void GetRidOfSessions_ValidSession(int nValid, int nNotValid)
        {
            for(int i=0; i< nNotValid; i++)
            {
                Site.CreateUser($"Username{i}", $"PassWord{i}");
                Site.Login($"Username{i}", $"PassWord{i}");
            }
            TheClock.AddHours(1);
            for (int i = 0; i < nValid; i++)
            {
                Site.CreateUser($"Username{i}{nNotValid}", $"PassWord{i}{nNotValid}");
                Site.Login($"Username{i}{nNotValid}", $"PassWord{i}{nNotValid}");
            }
            ((SiteLogic)Site).GetRidOfSessions();
            Assert.That(() => Site.ToyGetSessions(), Has.Count.EqualTo(nValid));
        }

    }
}
