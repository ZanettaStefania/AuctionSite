using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using TAP21_22_AuctionSite.Interface;

namespace Zanetta
{
    public class TapDbContext : TAP21_22_AuctionSite.Interface.TapDbContext {

        public TapDbContext()
        {
            ++NumberOfCreatedContexts;
        }

        public TapDbContext(string connectionString) : base(new DbContextOptionsBuilder<TapDbContext>()
            .UseSqlServer(connectionString).Options)
        {
            ++NumberOfCreatedContexts;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var s = modelBuilder.Entity<Session>();
            s.HasOne<User>(u => u.UserFrom).WithOne().OnDelete(DeleteBehavior.NoAction);
            
            var a = modelBuilder.Entity<Auction>();
            a.HasOne(u => u.Seller).WithMany().OnDelete(DeleteBehavior.NoAction);
            a.HasOne(u => u.CurrentWinner).WithMany().OnDelete(DeleteBehavior.NoAction);

            var u = modelBuilder.Entity<User>();

        }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Auction> Auction { get; set; }
    }

    public class User
    {
        public int UserId { get; set; }
        [MinLength(3)]
        [MaxLength(64)]
        public string Username { get; set; }
        [MinLength(4)]
        public string Password { get; set; }

        public int SiteId { get; set; }
        public Site Site { get; set; }

        public User(string username, string password, int siteId)
        {
            Username = username;
            Password = password;
            SiteId = siteId;
        }
    }

    public class Site
    {
        public int SiteId { get; set; }
        [MinLength(1)]
        [MaxLength(128)]
        public string Name { get; set; }
        public int Timezone { get; set; }
        public int SessionExpirationInSeconds { get; set; }
        public double MinimumBidIncrement { get; set; }

        public List<User> Users { get; set; }
        public List<Session> Sessions { get; set; }

        public Site(string name, int timezone, int sessionExpirationInSeconds, double minimumBidIncrement)
        {
            Name = name;
            Timezone = timezone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            Users = new List<User>();
            Sessions = new List<Session>();
        }
    }

    public class Session
    {
        public string Id { get; set; }
        public int UserId { get; set; }
        public User UserFrom { get; set; }
        public DateTime ValidUntil { get; set; }
        
       
        public int SiteId { get; set; }
        [ForeignKey("SiteId")]
        public Site Site { get; set; }

        public Session(string id, int userId, DateTime validUntil, int siteId)
        {
            Id = id;
            UserId = userId;
            ValidUntil = validUntil;
            SiteId = siteId;
        }
        
    }

    public class Auction
    {
        public int Id { get; set; }
        public int SellerId { get; set; }
        public int? CurrentWinnerId { get; set; }
        [ForeignKey("SellerId")]
        public User Seller { get; set; }
        [ForeignKey("CurrentWinnerId")]
        public User? CurrentWinner { get; set; }
        public double CurrentPrice { get; set; }
        public double HighestBid { get; set; }
        public string Description { get; set; }
        public DateTime EndsOn { get; set; }

        public Auction(int sellerId, double currentPrice, string description, DateTime endsOn)
        {
            SellerId = sellerId;
            CurrentPrice = currentPrice;
            Description = description;
            EndsOn = endsOn;
        }
    }
}
