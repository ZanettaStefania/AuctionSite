using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using TAP21_22.AlarmClock.Interface;
using TAP21_22_AuctionSite.Interface;


namespace Zanetta.Logic
{
    public class HostFactoryLogic : IHostFactory
    {
        public void CreateHost(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new AuctionSiteArgumentNullException(nameof(connectionString) + " is null or empty.");

            using (var c = new TapDbContext(connectionString))
            {
                try
                {
                    c.Database.EnsureDeleted();
                    c.Database.EnsureCreated();

                    if (!c.Database.CanConnect())
                        throw new AuctionSiteUnavailableDbException($"{nameof(connectionString)} can't be reached or is malformed.");
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException(
                        $"{nameof(connectionString)} can't be reached or is malformed.", e);
                }
                catch (DbException e)
                {
                    throw new AuctionSiteUnavailableDbException(
                        $"{nameof(connectionString)} can't be reached or is malformed.", e);
                }
            }
        }

        public IHost LoadHost(string connectionString, IAlarmClockFactory alarmClockFactory)
        {
            if (String.IsNullOrEmpty(connectionString))
                throw new AuctionSiteArgumentNullException($"{nameof(connectionString)} is null or empty.");

            if (alarmClockFactory == null)
                throw new AuctionSiteArgumentNullException($"{nameof(connectionString)} is null or empty.");

            using (var c = new TapDbContext(connectionString))
            {
                if (!(c.Database.CanConnect()))
                    throw new AuctionSiteUnavailableDbException(
                        $"{nameof(connectionString)} can't be reached or is malformed.");
            }


            var newHost = new HostLogic()
            {
                MyConnectionString = connectionString,
                MyAlarmClockFactory = alarmClockFactory
            };
            return newHost;
        }
    }
}