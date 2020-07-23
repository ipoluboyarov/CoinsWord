using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConsoleApp5.Entity
{
    public sealed class Country
    {
        public Country()
        {
            Neighbors = new List<Country>();
        }

        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Country> Neighbors { get; set; }
    }

    public sealed class Coin
    {
        public Coin()
        {
            Transactions = new List<Transaction>();
        }

        //[Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Key]
        [ForeignKey("Country")]
        public int Id { get; set; }
        public Country Country { get; set; }
        public ICollection<Transaction> Transactions { get; set; }
    }

    public class Transaction
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public virtual Country Sender { get; set; }
        public virtual Country Recipient { get; set; }
        public virtual Coin Coin { get; set; }
        public int Count { get; set; }
    }

    public class Balance
    {
        public virtual Coin Coin { get; set; }
        public int Count { get; set; }
    }
}
