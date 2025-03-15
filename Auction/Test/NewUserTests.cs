using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;


namespace TAP21_22.AuctionSite.Testing
{
    public class NewUserTests : AuctionSiteTests
    {
        /// <summary>
        ///     Initializes Site:
        ///     <list type="table">
        ///         <item>
        ///             <term>name</term>
        ///             <description>site for user tests</description>
        ///         </item>
        ///         <item>
        ///             <term>time zone</term>
        ///             <description>-5</description>
        ///         </item>
        ///         <item>
        ///             <term>expiration time</term>
        ///             <description>360 seconds</description>
        ///         </item>
        ///         <item>
        ///             <term>minimum bid increment</term>
        ///             <description>7</description>
        ///         </item>
        ///         <item>
        ///             <term>users</term>
        ///             <description>username = "My Dear Friend", pw = "f86d 78ds6^^^55"</description>
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
        public void Initialize()
        {
            Site = CreateAndLoadEmptySite(-5, "site for user tests", 360, 7, out TheClock);
            Site.CreateUser(UserName, Pw);
            User = Site.ToyGetUsers().Single(u => u.Username == UserName);
        }

        protected ISite Site = null!;
        protected IUser User = null!;
        protected const string UserName = "My Dear Friend";
        protected const string Pw = "f86d 78ds6^^^55";

        /// <summary>
        ///     Verify that the exception is thrown if we try to delete
        ///     the seller of an auction that is not ended
        /// </summary>
        [Test]
        public void DeleteUser_WithAuctionNotEnded_Throw()
        {
            ISession session = Site.Login(UserName, Pw);
            session.CreateAuction("A great object", Site.Now().AddHours(2), 50);
            Assert.That( () => User.Delete(), Throws.TypeOf<AuctionSiteInvalidOperationException>());
        }

        /// <summary>
        ///     Verify that when the user is deleted on a site
        ///     on other sites the user with same username still exist
        /// </summary>
        [Test]
        public void DeleteUser_CheckOnOtherSite()
        {
            ISite OtherSite = CreateAndLoadEmptySite(-5, "othersite", 360, 7, out TheClock);
            OtherSite.CreateUser(UserName, Pw);
            User.Delete();
            Assert.That(() => OtherSite.ToyGetUsers(), Has.Count.EqualTo(1));
        }

        /// <summary>
        ///     Verify that when the user is deleted
        ///     calling the method WonAuction throws InvalidOperation
        /// </summary>
        [Test]
        public void DeletedUser_WonAuction_Throw()
        {
            User.Delete();
            Assert.That(() => User.WonAuctions(), Throws.TypeOf<AuctionSiteInvalidOperationException>());
        }

        /// <summary>
        ///     Verify that when the user
        ///     calls the method WonAuction
        ///     it won't return not ended winning Auctions
        /// </summary>
        [Test]
        public void WonAuctions_On_StillOpenAuction_Returns_Empty()
        {
            ISession session = Site.Login(UserName, Pw);
            IAuction au = session.CreateAuction("A great object", Site.Now().AddHours(2), 50);
            Site.CreateUser("OtherUser", "Password");
            ISession session2 = Site.Login("OtherUser", "Password");
            au.Bid(session2, 100);
            Assert.That(() => Site.ToyGetUsers().Single(u => u.Username == "OtherUser").WonAuctions(), Is.Empty);
        }

    }
}
