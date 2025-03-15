using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TAP21_22_AuctionSite.Interface;

namespace TAP21_22.AuctionSite.Testing
{
    public class NewSessionTests : AuctionSiteTests
    {
        /// <summary>
        ///     Initializes Site:
        ///     <list type="table">
        ///         <item>
        ///             <term>name</term>
        ///             <description>site for session tests</description>
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
        ///             <description> User (with UserName = "My Dear Friend" and Pw = "f86d 78ds6^^^55") </description>
        ///         </item>
        ///         <item>
        ///             <term>auctions</term>
        ///             <description>empty list</description>
        ///         </item>
        ///         <item>
        ///             <term>sessions</term>
        ///             <description>Session for User</description>
        ///         </item>
        ///     </list>
        /// </summary>
        [SetUp]
        public void Initialize()
        {
            Site = CreateAndLoadSite(-5, "site for session tests", 360, 7, out TheClock);
            Site.CreateUser(UserName, Pw);
            User = Site.ToyGetUsers().Single(u => u.Username == UserName);
            var session = Site.Login(UserName, Pw);
            if (null == session)
                Assert.Inconclusive($"The user {UserName} should have been able to log in with password {Pw}");
            Session = session!;
        }

        protected ISite Site = null!;
        protected IUser User = null!;
        protected ISession Session = null!;
        protected const string UserName = "My Dear Friend";
        protected const string Pw = "f86d 78ds6^^^55";


        /// <summary>
        ///     Verify that calling Logout on a expired Session
        ///     throws AuctionSiteInvalidOperationException
        /// </summary>
        [Test]
        public void NotValidSession_Throw()
        {
            TheClock.Now = TheClock.Now.AddHours(3);
            Assert.That(() => Session.Logout(), Throws.TypeOf<AuctionSiteInvalidOperationException>());
        }

        /// <summary>
        ///     Verify that calling Logout on already Logout session
        ///     throws AuctionSiteArgumentException
        /// </summary>
        [Test]
        public void AlreadyLogOutSession_Throw()
        {
            Session.Logout();
            Assert.That(() => Session.Logout(), Throws.TypeOf<AuctionSiteArgumentException>());
        }

    }
}
