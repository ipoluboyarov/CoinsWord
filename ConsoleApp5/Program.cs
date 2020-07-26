using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp5.Entity;

namespace ConsoleApp5
{
    internal class Program
    {
        private static void Main()
        {
            using (var db = new CountryContext())
            {
                CreateWorld(db);
                bool isFull;
                var currentDate = Convert.ToDateTime("01/01/2020");
                var countries = db.Countries.ToList();
                do
                {
                    isFull = true;
                    //каждый год создаем монеты
                    if (currentDate.DayOfYear == 1)
                    {
                        CreateCoins(db, currentDate, 20);
                        Console.WriteLine("**********************************************************");
                    }

                    var newAllTransactions = new List<Transaction>();

                    foreach (var country in countries)
                    {
                        var newCountryTransactions = new List<Transaction>();
                        var balanceCountry = GetBalance(db, country.Id, currentDate);

                        if (balanceCountry.Count() < 100) isFull = false;

                        var balanceLineX = GetBalanceLine(db, country.Id, currentDate, true);
                        var balanceLineY = GetBalanceLine(db, country.Id, currentDate, false);

                        var forLineX = balanceCountry.Where(x => (x.Count > 1 || x.Coin.Country.Id == country.Id)).OrderByDescending(o => o.Count).Select(x => x.Coin)
                            .Except(balanceLineX.Where(x => x.Count > 8).Select(x => x.Coin)).ToList();

                        var forLineY = balanceCountry.Where(x => (x.Count > 1 || x.Coin.Country.Id == country.Id)).OrderByDescending(o => o.Count).Select(x => x.Coin)
                            .Except(balanceLineY.Where(x => x.Count > 8).Select(x => x.Coin)).ToList();

                        //торгуем монетами нужными только по горизонтали
                        var forOnlyX = forLineX.Except(forLineY).ToList();
                        newCountryTransactions.AddRange(Trade(country,
                            country.Neighbors.Where(n => Math.Abs(n.Id - country.Id) < 10).ToList(), balanceCountry,
                            forOnlyX, newCountryTransactions.Sum(x => x.Count),
                            currentDate));

                        //торгуем монетами нужными только по вертикали
                        var forOnlyY = forLineY.Except(forLineX).ToList();
                        newCountryTransactions.AddRange(Trade(country,
                            country.Neighbors.Where(n => Math.Abs(n.Id - country.Id) >= 10).ToList(),
                            balanceCountry,
                            forOnlyY, newCountryTransactions.Sum(x => x.Count),
                            currentDate));

                        //распределяем оставшиеся монеты по всем
                        var forAllOther = balanceCountry.Where(x => (x.Count > 1 || x.Coin.Country.Id == country.Id)).OrderByDescending(o => o.Count).Select(x => x.Coin)
                            .Except(forOnlyX)
                            .Except(forOnlyY);
                        newCountryTransactions.AddRange(Trade(country, country.Neighbors.ToList(), balanceCountry,
                            forAllOther, newCountryTransactions.Sum(x => x.Count),
                            currentDate));

                        newAllTransactions.AddRange(newCountryTransactions);
                    }

                    //TODO: bulkinsert
                    db.Transactions.AddRange(newAllTransactions.Where(x => x.Count > 0));
                    db.SaveChanges();

                    //Отчет по стране
                    foreach (var country in countries)
                    {
                        var balanceCountryStart = GetBalance(db, country.Id, currentDate);
                        var balanceCountryEnd = GetBalance(db, country.Id, currentDate.AddMonths(1));

                        var tradeIn = newAllTransactions.Where(x => x.Recipient.Id == country.Id).Sum(s => s.Count);
                        var tradeOut = newAllTransactions.Where(x => x.Sender.Id == country.Id).Sum(s => s.Count);

                        Console.WriteLine("*******************************" +
                                          $"\r\n{currentDate} \r\n" +
                                          $"Country = {country.Id} \r\n" +
                                          $"Balance start = {balanceCountryStart.Sum(x => x.Count)}\r\n" +
                                          $"TradeOut = {tradeOut}\r\n" +
                                          $"TradeIn = {tradeIn}\r\n" +
                                          $"Balance end = {balanceCountryEnd.Sum(x => x.Count)}");
                        foreach (var balance in balanceCountryEnd.OrderBy(x => x.Coin.Id))
                            Console.WriteLine($"Coin({balance.Coin.Country.Id}) = {balance.Count}");
                    }

                    currentDate = currentDate.AddMonths(1);
                } while (!isFull);
            }

            Console.Read();
        }


        public static List<Transaction> Trade(Country country, List<Country> tradeNeighbors,
            List<Balance> balanceCountry, IEnumerable<Coin> tradeCoins, int countSendCoins, DateTime currentDate)
        {
            var transactions = new List<Transaction>();
            var sum = balanceCountry.Select(x => x.Count).Sum();

            //Цикл по необходимым номиналам монет
            foreach (var balance in balanceCountry.Where(x => tradeCoins.Contains(x.Coin)))
            {
                if (countSendCoins >= sum / 2) return transactions;

                //одна монета у страны должна остаться
                balance.Count--;

                //исключаем страну монеты из распределения 
                var neighborsWithoutCoinsOwner = tradeNeighbors.Where(x => x != balance.Coin.Country).ToList();

                //не превышаем половины бюджета
                if (balance.Count + countSendCoins > sum / 2) balance.Count = sum / 2 - countSendCoins;

                //распределяем все монеты( + первым соседям распределяем остаток)
                var cntCoin = balance.Count / neighborsWithoutCoinsOwner.Count();
                var remCoin = balance.Count % neighborsWithoutCoinsOwner.Count();

                for (var i = 0; i < neighborsWithoutCoinsOwner.Count(); i++)
                {
                    transactions.Add(new Transaction()
                    {
                        Date = currentDate,
                        Coin = balance.Coin,
                        Sender = country,
                        Recipient = neighborsWithoutCoinsOwner[i],
                        Count = cntCoin + (remCoin > i ? 1 : 0)
                    });
                }

                countSendCoins += balance.Count;
            }

            return transactions.Where(x => x.Count > 0).ToList();
        }


        public static List<Balance> GetBalanceLine(CountryContext db, int countryId, DateTime date, bool xLine)
        {
            var start = xLine ? countryId - countryId % 10 : countryId / 10 * 10;
            var end = start + (xLine ? 9 : 90);
            var step = xLine ? 1 : 10;

            var ids = new List<int>();
            for (var i = start; i <= end; i += step)
            {
                if (i == countryId) continue;
                ids.Add(i);
            }

            return db.Transactions
                .Where(x => (ids.Contains(x.Sender.Id) || ids.Contains(x.Recipient.Id)) && x.Date < date)
                .Select(x => new Balance { Coin = x.Coin, Count = x.Count * (ids.Contains(x.Recipient.Id) ? 1 : -1) })
                .GroupBy(x => x.Coin)
                .Select(x => new Balance
                {
                    Coin = x.Key,
                    Count = x.Sum(s => s.Count)
                }).Where(b => b.Count > 0).ToList();
        }

        public static List<Balance> GetBalance(CountryContext db, int countryId, DateTime date)
        {
            return db.Transactions
                .Where(x => (x.Sender.Id == countryId || x.Recipient.Id == countryId) && x.Date < date)
                .Select(x => new Balance { Coin = x.Coin, Count = x.Count * (x.Recipient.Id == countryId ? 1 : -1) })
                .GroupBy(x => x.Coin)
                .Select(x => new Balance
                {
                    Coin = x.Key,
                    Count = x.Sum(s => s.Count)
                }).Where(b => b.Count > 0).ToList();
        }

        public static void CreateCoins(CountryContext db, DateTime dateTime, int countCoins)
        {
            var transactions = new List<Transaction>();

            foreach (var country in db.Countries.ToList())
                transactions.Add(new Transaction
                {
                    Coin = db.Coins.First(x => x.Country.Id == country.Id),
                    Recipient = country,
                    Sender = null,
                    Date = dateTime.AddMonths(-1),
                    Count = countCoins
                });

            db.Transactions.AddRange(transactions);
            db.SaveChanges();
        }

        private static void CreateWorld(CountryContext db)
        {
            var countries = new List<Country>();

            for (var i = 0; i < 100; i++) countries.Add(new Country { Id = i, Name = i.ToString() });

            db.Countries.AddRange(countries);

            foreach (var country in countries)
            {
                if ((country.Id + 1) % 10 == 0)
                {
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id - 9));
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id - 1));
                }
                else if (country.Id % 10 == 0)
                {
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id + 9));
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id + 1));
                }
                else
                {
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id + 1));
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id - 1));
                }

                if (country.Id >= 90)
                {
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id - 90));
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id - 10));
                }
                else if (country.Id < 10)
                {
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id + 90));
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id + 10));
                }
                else
                {
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id + 10));
                    country.Neighbors.Add(countries.First(x => x.Id == country.Id - 10));
                }

                db.Coins.Add(new Coin { Id = country.Id, Country = country });

                db.SaveChanges();
            }
        }
    }
}