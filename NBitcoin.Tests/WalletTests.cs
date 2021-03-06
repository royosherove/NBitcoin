﻿using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NBitcoin.Tests
{
	public class WalletTests
	{
		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanLockAndUnlockMoney()
		{
			WalletTester tester = new WalletTester();
			Account account = new Account();

			var chain = new Chain(Network.Main);
			var fork = new Chain(Network.Main);
			
			//tester.GiveMoney("1.0", chain, fork);
			tester.GiveMoney("0.5", chain, fork);

			tester.UpdateWallet(chain);
			Assert.NotNull(tester.Wallet.Accounts.Available.GetEntriesToCover("0.5", false));

			var spendable = tester.Wallet.Accounts.Available.GetEntriesToCover("0.5", true);
			Assert.NotNull(spendable);
			Assert.Null(tester.Wallet.Accounts.Available.GetEntriesToCover("0.5", true));
			Assert.True(tester.Wallet.Accounts.Available.Locked.Count() == 1);
			spendable[0].IsLocked = false;
			Assert.True(tester.Wallet.Accounts.Available.Locked.Count() == 0);
		}


		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanManageMoney()
		{
			WalletTester tester = new WalletTester();
			Chain chain = new Chain(Network.Main);
			//Change C,A,U
			tester.GiveMoney("1.0", chain);
			Assert.True(tester.UpdateWallet(chain));
			tester.AssertPools(
				"+1.00",
				"+1.00",
				"+1.00");
			Assert.False(tester.UpdateWallet(chain));

			//Change U
			var rcv = tester.GiveMoney("0.1", null);
			tester.AssertPools(
				"+1.00",
				"+1.00",
				"+1.00+0.10");

			tester.AppendBlock(rcv, chain);
			//Change C,A
			Assert.True(tester.UpdateWallet(chain));
			tester.AssertPools(
				"+1.00+0.10",
				"+1.00+0.10",
				"+1.00+0.10");

			//Change A,U
			var pay = tester.Pay("0.1", false, null);
			tester.AssertPools(
				"+1.00+0.10",
				"+1.00+0.10-0.10",
				"+1.00+0.10-0.10");

			//Chance C
			tester.AppendBlock(pay, chain);
			tester.UpdateWallet(chain);
			tester.AssertPools(
				"+1.00+0.10-0.10",
				"+1.00+0.10-0.10",
				"+1.00+0.10-0.10");

			Assert.False(tester.UpdateWallet(chain));
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void WalletFinishInCorrectStateWhenDoubleSpending()
		{
			WalletTester tester = new WalletTester();
			Chain chain = new Chain(Network.Main);

			tester.GiveMoney("1.0", chain);
			tester.GiveMoney("0.5", chain);
			tester.UpdateWallet(chain);
			tester.AssertPools(
				"+1.00+0.50",
				"+1.00+0.50",
				"+1.00+0.50");

			//Only verified
			tester.Pay("0.5", true, null);
			tester.AssertPools(
				"+1.00+0.50",
				"+1.00+0.50-0.50",
				"+1.00+0.50-0.50");

			//Double spending of the 0.5 coin, confirmed
			tester.Pay("1.5", true, chain);
			tester.UpdateWallet(chain);
			tester.AssertPools(
				"+1.00+0.50-0.50-1.00",
				"+1.00+0.50-0.50-1.00",
			"+1.00+0.50-0.50-1.00");
		}

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void CanManageMoneyInFork()
		{
			WalletTester tester = new WalletTester();
			var chain = new Chain(Network.Main);
			var fork = new Chain(Network.Main);
			//Change C,A,U
			tester.GiveMoney("1.0", chain, fork);
			tester.GiveMoney("2.0", chain, fork);
			tester.GiveMoney("3.0", chain, fork);
			var four = tester.GiveMoney("4.0", fork);

			tester.UpdateWallet(fork);
			tester.AssertPools(
				"+1.00+2.00+3.00+4.00",
				"+1.00+2.00+3.00+4.00",
				"+1.00+2.00+3.00+4.00");

			//4.0 canceled
			tester.UpdateWallet(chain);
			tester.AssertPools(
			"+1.00+2.00+3.00+4.00-4.00",
			"+1.00+2.00+3.00+4.00-4.00",
			"+1.00+2.00+3.00+4.00"); //4.0 is in mempool


			//4.0 re introduced
			tester.UpdateWallet(fork);
			tester.AssertPools(
			"+1.00+2.00+3.00+4.00-4.00+4.00",
			"+1.00+2.00+3.00+4.00-4.00+4.00",
			"+1.00+2.00+3.00+4.00");

			//4.0 canceled, 5.0 intro
			tester.GiveMoney("5.0", chain);
			tester.UpdateWallet(chain);
			tester.AssertPools(
			"+1.00+2.00+3.00+4.00-4.00+4.00-4.00+5.00",
			"+1.00+2.00+3.00+4.00-4.00+4.00-4.00+5.00",
			"+1.00+2.00+3.00+4.00+5.00");


			//spend all
			tester.Pay(tester.Wallet.Accounts.Confirmed.Balance, true, chain);
			tester.UpdateWallet(chain);
			Assert.Equal(Money.Zero, tester.Wallet.Accounts.Confirmed.Balance);
			Assert.Equal(Money.Zero, tester.Wallet.Accounts.Available.Balance);
			Assert.Equal(Money.Parse("4.00"), tester.Wallet.Accounts.Unconfirmed.Balance);

			//the canceled transaction (forked) become confirmed
			tester.AppendBlock(four, chain);
			tester.UpdateWallet(chain);
			Assert.Equal(Money.Parse("4.00"), tester.Wallet.Accounts.Confirmed.Balance);
			Assert.Equal(Money.Parse("4.00"), tester.Wallet.Accounts.Available.Balance);
			Assert.Equal(Money.Parse("4.00"), tester.Wallet.Accounts.Unconfirmed.Balance);

			var unconfirmedSpent = tester.Pay(Money.Parse("4.00"), true, null);
			Assert.Equal(Money.Parse("4.00"), tester.Wallet.Accounts.Confirmed.Balance);
			Assert.Equal(Money.Parse("0.00"), tester.Wallet.Accounts.Available.Balance);
			Assert.Equal(Money.Parse("0.00"), tester.Wallet.Accounts.Unconfirmed.Balance);

			tester.AppendBlock(unconfirmedSpent, chain);
			tester.UpdateWallet(chain);
			Assert.Equal(Money.Parse("0.00"), tester.Wallet.Accounts.Confirmed.Balance);
			Assert.Equal(Money.Parse("0.00"), tester.Wallet.Accounts.Available.Balance);
			Assert.Equal(Money.Parse("0.00"), tester.Wallet.Accounts.Unconfirmed.Balance);
		}


	}
}