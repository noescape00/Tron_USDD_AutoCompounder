using System.Numerics;
using DeFi_Strategies.Helpers;
using Nethereum.HdWallet;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using NLog;
using RestSharp;
using TronNet;
using TronNet.Accounts;

namespace DeFi_Strategies.Tron.CompoundUSDD
{
    public class AutoCompounder
    {
        public const string GaugeAddress = "TCkNadwxyik1D66qCGmavuneowDRXPgdkL";

        public const string SunswapRouterAddress = "TKzxdSv2FZKQrEqkKVgp5DcwEXBEKMg2Ax";

        public const string USDD_USDT_lpPairAddress = "TNLcz8A9hGKbTNJ6b6C1GTyigwxURbWzkM";

        public const string TrongridApiEndpoint = "https://api.trongrid.io/v1/";

        private readonly string accountMnemonic;
        private readonly double claimThresholdUSDD;

        private readonly string accountAddress;
        private readonly ITronAccount account;
        private readonly IWalletClient wallet;
        private readonly ITransactionClient transactionClient;
        private readonly Account mainAccount;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public AutoCompounder(IWalletClient wallet, ITransactionClient transactionClient, CompoundingConfig config)
        {
            this.wallet = wallet;
            this.transactionClient = transactionClient;

            this.accountMnemonic = config.AccountMnemonic;
            this.claimThresholdUSDD = config.ClaimThresholdUSDD;

            // Setup account
            Wallet wallet1 = new Wallet(accountMnemonic, null, "m/44'/195'/0'/0");
            this.mainAccount = wallet1.GetAccount(0);

            this.account = wallet.GetAccount(mainAccount.PrivateKey);
            this.accountAddress = account.Address;
        }

        public async Task AutocompoundAsync()
        {
            this.logger.Info("Autocompounding started. Your address: " + accountAddress);
            SuperGaugeContractClient gauge = new SuperGaugeContractClient(this.wallet, this.transactionClient, GaugeAddress, accountAddress, this.mainAccount.PrivateKey);
            SunswapV2Router02 router = new SunswapV2Router02(this.wallet, this.transactionClient, SunswapRouterAddress, this.accountAddress, this.mainAccount.PrivateKey);

            while (true)
            {
                try
                {
                    double claimableUSDD = await gauge.GetClaimableRewardsAsync();

                    this.logger.Info("Claimable USDD: {0}", claimableUSDD);

                    if (claimableUSDD < claimThresholdUSDD)
                        logger.Info(string.Format("Claimable USDD ({0}) is less than threshold ({1}). Not claiming.", claimableUSDD, claimThresholdUSDD));
                    else
                    {
                        this.logger.Info(string.Format("Claiming {0} USDD rewards and waiting 30 sec for state update...", claimableUSDD));
                        string claimTxId = await gauge.ClaimRewardsAsync();

                        if (claimTxId == null)
                            this.logger.Warn("Swap tx id is null! Error");
                        else
                            this.logger.Debug("Claim txid: " + claimTxId);

                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }

                    AccountBalance balanceInfo = await this.GetBalancesInfoAsync();
                    balanceInfo.Log(this.logger);

                    if ((double)balanceInfo.Balance_USDD < claimThresholdUSDD)
                    {
                        this.logger.Info(string.Format("USDD balance is less than threshold of {0}. Waiting 60 minutes for next iteration...", claimableUSDD));
                        await Task.Delay(TimeSpan.FromMinutes(60));
                        continue;
                    }

                    var swapAmount = balanceInfo.Balance_USDD / 2;
                    this.logger.Info("Swapping half of USDD ({0}) for USDT and waiting 30 sec for state update...", swapAmount);

                    string swapTxId = await router.SwapUSDDforUSDTAsync(swapAmount);

                    if (swapTxId == null)
                        this.logger.Warn("Swap tx id is null! Error");
                    else
                        this.logger.Debug("Swap txid: " + swapTxId);

                    await Task.Delay(TimeSpan.FromSeconds(30));

                    // Get LP reserves
                    SunswapV2Router02.getReservesOutput reserves = await router.GetReservesAsync(USDD_USDT_lpPairAddress);

                    double USDD_reserve = reserves._reserve0.DivideToDouble((BigInteger)Math.Pow(10, 18));
                    double USDT_reserve = reserves._reserve1.DivideToDouble((BigInteger)Math.Pow(10, 6));

                    double USDD_price_in_USDT = USDT_reserve / USDD_reserve;

                    this.logger.Info("USDD-USDT LP reserves: {0} : {1}; USDD price in USDT: {2}", USDD_reserve, USDT_reserve, USDD_price_in_USDT);

                    balanceInfo = await this.GetBalancesInfoAsync();
                    balanceInfo.Log(this.logger);

                    // Add liquidity
                    decimal USDD_to_add, USDT_to_add;

                    if (balanceInfo.Balance_USDT < balanceInfo.Balance_USDT * (decimal) USDD_price_in_USDT)
                    {
                        USDT_to_add = balanceInfo.Balance_USDT;
                        USDD_to_add = USDT_to_add / (decimal)USDD_price_in_USDT;
                    }
                    else
                    {
                        USDD_to_add = balanceInfo.Balance_USDD;
                        USDT_to_add = USDD_to_add * (decimal) USDD_price_in_USDT;
                    }

                    this.logger.Info("Adding liquidity to pool. {0} USDD and {1} USDT", USDD_to_add, USDT_to_add);

                    string addLiquidityTxId = await router.AddLiquidityAsync(USDD_to_add, USDT_to_add);

                    if (addLiquidityTxId == null)
                        this.logger.Warn("Add liquidity tx id is null! Error");
                    else
                        this.logger.Debug("Add liquidity txid: " + addLiquidityTxId);

                    await Task.Delay(TimeSpan.FromSeconds(30));

                    balanceInfo = await this.GetBalancesInfoAsync();
                    balanceInfo.Log(this.logger);

                    if (balanceInfo.Ballance_USDD_USDT_LP > 0)
                    {
                        logger.Info("Depositing {0} LP tokens.", balanceInfo.Ballance_USDD_USDT_LP);

                        string depositLPTxId = await gauge.DepositLPTokensAsync(balanceInfo.Ballance_USDD_USDT_LP);

                        if (depositLPTxId == null)
                            this.logger.Warn("Deposit LP tx id is null! Error");
                        else
                            this.logger.Debug("Deposit LP txid: " + depositLPTxId);
                    }
                    else
                        logger.Info("No LP tokens found, skipping.");
                }
                catch (Exception e)
                {
                    this.logger.Error(e.ToString());
                }

                logger.Info("Waiting 60 minutes...");
                await Task.Delay(TimeSpan.FromMinutes(60));
            }
        }

        private async Task<AccountBalance> GetBalancesInfoAsync()
        {
            Uri accInfoEndpoint = new Uri(TrongridApiEndpoint + "accounts/" + accountAddress + "?only_confirmed=true");
            RestClient client = new RestClient(accInfoEndpoint);

            RestRequest request = new RestRequest(accInfoEndpoint);
            request.AddHeader("Accept", "application/json");

            RestResponse response = await client.ExecuteAsync<AccountInfoRoot>(request);

            AccountInfoRoot rootObj = JsonConvert.DeserializeObject<AccountInfoRoot>(response.Content);

            var data = rootObj.data.First();

            decimal trxBalance = (decimal)data.balance / 1000000;

            string usddBalanceString = data.trc20.FirstOrDefault(x => x.TPYmHEhy5n8TCEfYGqW2rPxsghSfzghPDn != null)?.TPYmHEhy5n8TCEfYGqW2rPxsghSfzghPDn;
            decimal usddBalance = usddBalanceString == null ? 0 : decimal.Parse(usddBalanceString) / 1000000000000000000;

            string usdtBalanceString = data.trc20.FirstOrDefault(x => x.TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t != null)?.TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t;
            decimal usdtBalance = usdtBalanceString == null? 0 : decimal.Parse(usdtBalanceString) / 1000000;

            string lpBalanceString = data.trc20.FirstOrDefault(x => x.TNLcz8A9hGKbTNJ6b6C1GTyigwxURbWzkM != null)?.TNLcz8A9hGKbTNJ6b6C1GTyigwxURbWzkM;
            decimal lpBalance = lpBalanceString == null ? 0 : decimal.Parse(lpBalanceString) / 1000000000000000000;

            return new AccountBalance()
            {
                Balance_TRX = trxBalance,
                Balance_USDD = usddBalance,
                Balance_USDT = usdtBalance,
                Ballance_USDD_USDT_LP = lpBalance
            };
        }
    }

    internal class AccountBalance
    {
        public decimal Balance_USDT, Balance_USDD, Balance_TRX, Ballance_USDD_USDT_LP;

        public void Log(Logger logger)
        {
            logger.Info("Balance_USDT: {0}  Balance_USDD: {1}  Balance_TRX: {2} USDD_USDT_LP: {3}", Balance_USDT, Balance_USDD, Balance_TRX, Ballance_USDD_USDT_LP);
        }
    }
}
