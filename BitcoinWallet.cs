using System;
using System.Collections;
using System.Globalization;
using System.Text;
using HBitcoin.FullBlockSpv;
using HBitcoin.KeyManagement;
using HBitcoin.Models;
using NBitcoin;
using QBitNinja.Client;
using QBitNinja.Client.Models;
using static System.Console;

namespace Simple_Bitcoin_Wallet
{
    class BitcoinWallet
    {
        static void Main(string[] args)
        {
            string[] valiableOperations = {
                "create", "recover", "balance", "history","receive","send", "exit" //Allowed functionality
            };
            string input = string.Empty;
            while (!input.ToLower().Equals("exit"))
            {
                do 
                {
                    Write("Enter operation [\"Create\", \"Recover\", \"Balance\", \"History\", \"Receive\", \"Send\", \"Exit\"]: ");
                    input = ReadLine().ToLower().Trim();
                } while (!((IList) valiableOperations).Contains(input));
                switch (input)
                {
                    case "create":
                        CreateWallet();
                        break;
                    case "recover":
                        WriteLine(
                              "Please note the wallet cannot check if your password is correct or not. " +
                              "If you provide a wrong password a wallet will be recovered with your " +
                              "provided mnemonic AND password pair: ");
                        Write("Enter password: ");
                        string pw = ReadLine();
                        Write("Enter mnemonic phrase: ");
                        string mnemonic = ReadLine();
                        Write("Enter date (yyyy-MM-dd): ");
                        string date = ReadLine();
                        Mnemonic mnem = new Mnemonic(mnemonic);
                        RecoverWallet(pw, mnem, date);
                        break;
                    case "receive":
                        Write("Enter wallet's name: ");
                        String walletName = ReadLine();
                        Write("Enter password: ");
                        pw = ReadLine();
                        Receive(pw, walletName);
                        break;
                    case "balance":
                        Write("Enter wallet's name: ");
                        walletName = ReadLine();
                        Write("Enter password: ");
                        pw = ReadLine();
                        Write("Enter wallet address: ");
                        string wallet = ReadLine();
                        ShowBalance(pw, walletName, wallet);
                        break;
                    case "history":
                        Write("Enter wallet's name: ");
                        walletName = ReadLine();
                        Write("Enter password: ");
                        pw = ReadLine();
                        Write("Enter wallet address: ");
                        wallet = ReadLine();
                        ShowHistory(pw, walletName, wallet);
                        break;
                    case "send":
                        Write("Enter wallet's name: ");
                        walletName = ReadLine();
                        Write("Enter wallet password: ");
                        pw = ReadLine();
                        Write("Enter wallet address: ");
                        wallet = ReadLine();
                        Write("Select outpoint (transaction ID): ");
                        string outPoint = ReadLine();
                        Send(pw, walletName, wallet, outPoint);
                        break;
                }
            }
        }

        private static void Send(string password, string walletName, string wallet, string outPoint)
        {
            string walletFilePath = @"Wallets\";
            NBitcoin.BitcoinExtKey privateKey = null;
            try
            {
                Safe loadedSafe = Safe.Load(password, walletFilePath + walletName + ".json");
                for (int i = 0; i < 10; i++)
                {
                    if (loadedSafe.GetAddress(i).ToString() == wallet)
                    {
                        Write("Enter private key: ");
                        privateKey = new BitcoinExtKey(ReadLine());
                        if (!privateKey.Equals(loadedSafe.FindPrivateKey(loadedSafe.GetAddress(i))))
                        {
                            WriteLine("Wrong private key!");
                            return;
                        }
                        break;
                    }
                }

            }
            catch
            {
                WriteLine("Wrong wallet or password!");
                return;
            }
            QBitNinjaClient client = new QBitNinjaClient(Network.TestNet);
            var balance = client.GetBalance(BitcoinAddress.Create(wallet), false).Result;
            OutPoint outPointToSpend = null;
            foreach (var entry in balance.Operations)
            {
                foreach (var coin in entry.ReceivedCoins)
                {
                    if (coin.Outpoint.ToString().Substring(0, coin.Outpoint.ToString().Length - 2) == outPoint)
                    {
                        outPointToSpend = coin.Outpoint;
                        break;
                    }
                }
            }
            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = outPointToSpend
            });
            Write("Enter address to send to: ");
            string addressToSentTo = ReadLine();
            var hallOfTheMakersAddress = BitcoinAddress.Create(addressToSentTo);
            Write("Enter amount to send: ");
            decimal amountToSend = decimal.Parse(ReadLine());
            TxOut hallOfTheMakersTxOut = new TxOut()
            {
                Value = new Money(amountToSend, MoneyUnit.BTC),
                ScriptPubKey = hallOfTheMakersAddress.ScriptPubKey
            };
            Write("Enter amount to get back: ");
            decimal amountToGetBack = decimal.Parse(ReadLine());
            TxOut changeBackTxOut = new TxOut()
            {
                Value = new Money(amountToGetBack, MoneyUnit.BTC),
                ScriptPubKey = privateKey.ScriptPubKey
            };
            transaction.Outputs.Add(hallOfTheMakersTxOut);
            transaction.Outputs.Add(changeBackTxOut);
            Write("Enter message: ");
            var message = ReadLine();
            var bytes = Encoding.UTF8.GetBytes(message);
            transaction.Outputs.Add(new TxOut()
            {
                Value = Money.Zero,
                ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
            });
            transaction.Inputs[0].ScriptSig = privateKey.ScriptPubKey;
            transaction.Sign(privateKey, false);
            BroadcastResponse broadcastResponse = client.Broadcast(transaction).Result;
            if (broadcastResponse.Success)
            {
                Console.WriteLine("Transaction send!");
            }
            else
            {
                Console.WriteLine("something went worng!:-(");
            }
        }

        private static void ShowHistory(string password, string walletName, string wallet)
        {
            string walletFilePath = @"Wallets\";
            try 
            {
                Safe loadedSafe = Safe.Load(password, walletFilePath + walletName + ".json");
            }
            catch
            {
                WriteLine("Wrong wallet or password!");
                return;
            }
            QBitNinjaClient client = new QBitNinjaClient(Network.TestNet); 
            var coinsReceived = client.GetBalance(BitcoinAddress.Create(wallet), true).Result;
            string header = "-----COINS RECEIVED-----";
            WriteLine(header);
            foreach (var entry in coinsReceived.Operations)
            {
                foreach (var coin in entry.ReceivedCoins)
                {
                    Money amount = (Money)coin.Amount;
                    WriteLine($"Transaction ID: {coin.Outpoint}; Received coins: {amount.ToDecimal(MoneyUnit.BTC)}");
                }
            }
            WriteLine(new string('-', header.Length));
            var coinsSpent = client.GetBalance(BitcoinAddress.Create(wallet), false).Result;
            string footer = "-----COINS SPENT-----";
            WriteLine(footer);
            foreach (var entry in coinsSpent.Operations)
            {
                foreach (var coin in entry.SpentCoins)
                {
                    Money amount = (Money)coin.Amount;
                    WriteLine($"Transaction ID: {coin.Outpoint}; Spent coins: {amount.ToDecimal(MoneyUnit.BTC)}");
                }
            }
            WriteLine(new string('-', footer.Length));
        }

        private static void ShowBalance(string pw, string walletName, string wallet)
        {
            string walletFilePath = @"Wallets\"; 
            try
            {
                Safe loadedSafe = Safe.Load(pw, walletFilePath + walletName + ".json");
            }
            catch
            {
                WriteLine("Wrong wallet or password!");
                return;
            }
            QBitNinjaClient client = new QBitNinjaClient(Network.TestNet); 
            decimal totalBalance = 0;
            var balance = client.GetBalance(BitcoinAddress.Create(wallet), true).Result; 
            foreach (var entry in balance.Operations)
            {
                foreach (var coin in entry.ReceivedCoins) 
                {
                    Money amount = (Money)coin.Amount;
                    decimal currentAmount = amount.ToDecimal(MoneyUnit.BTC);
                    totalBalance += currentAmount;
                }
            }
            WriteLine($"Balance of wallet: {totalBalance}");
        }

        private static void Receive(string password, string walletName)
        {
            string walletFilePath = @"Wallets\"; 
            try 
            {
                Safe loadedSafe = Safe.Load(password, walletFilePath + walletName + ".json");
                for (int i = 0; i < 10; i++)
                {
                    WriteLine(loadedSafe.GetAddress(i));
                }
            }
            catch (Exception)
            {
                WriteLine("Wallet with such name does not exist!");
            }
        }

        private static void RecoverWallet(string password, Mnemonic mnemonic, string date)
        {
            Network currentNetwork = Network.TestNet; 
            string walletFilePath = @"Wallets\"; 
            Random rand = new Random(); 
            Safe safe = Safe.Recover(mnemonic, password, walletFilePath + "RecoveredWalletNum" + rand.Next() + ".json", currentNetwork,
                creationTime: DateTimeOffset.ParseExact(date,
                    "yyyy-MM-dd", CultureInfo.InvariantCulture));
            WriteLine("Wallet successfully recovered");
        }

        private static void CreateWallet()
        {
            Network currentNetwork = Network.TestNet;
            string walletFilePath = @"Wallets\";
            string pw;
            string pwConfirmed;
            do
            {
                Write("Enter password: ");
                pw = ReadLine();
                Write("Confirm password: ");
                pwConfirmed = ReadLine();
                if (pw != pwConfirmed)
                {
                    WriteLine("Passwords did not match!");
                    WriteLine("Try again.");
                }
            } while (pw != pwConfirmed);
            bool failure = true;
            while (failure)
            {
                try
                {
                    Write("Enter wallet name:");
                    string walletName = ReadLine();
                    Mnemonic mnemonic;
                    Safe safe = Safe.Create(out mnemonic, pw, walletFilePath + walletName + ".json", currentNetwork);
                    WriteLine("Wallet created successfully");
                    WriteLine("Write down the following mnemonic words.");
                    WriteLine("With the mnemonic words AND the password you can recover this wallet.");
                    WriteLine();
                    WriteLine("----------");
                    WriteLine(mnemonic);
                    WriteLine("----------");
                    WriteLine(
                        "Write down and keep in SECURE place your private keys. Only through them you can access your coins!");
                    for (int i = 0; i < 10; i++)
                    {
                        WriteLine($"Address: {safe.GetAddress(i)} -> Private key: {safe.FindPrivateKey(safe.GetAddress(i))}");
                    }
                    failure = false;
                }
                catch
                {
                    WriteLine("Wallet already exists");
                }
            }
        }
    }
}