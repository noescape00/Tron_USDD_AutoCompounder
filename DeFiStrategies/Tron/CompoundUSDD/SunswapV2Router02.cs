using Google.Protobuf;
using NBitcoin.DataEncoders;
using NLog;
using ServiceStack.Text;
using TronNet;
using TronNet.ABI;
using TronNet.ABI.FunctionEncoding;
using TronNet.ABI.FunctionEncoding.Attributes;
using TronNet.ABI.Model;
using TronNet.Contracts;
using TronNet.Protocol;
using Base58Encoder = TronNet.Crypto.Base58Encoder;
using BigInteger = System.Numerics.BigInteger;

namespace DeFi_Strategies.Tron.CompoundUSDD
{
    public class SunswapV2Router02
    {
        private const string USDD_address = "TPYmHEhy5n8TCEfYGqW2rPxsghSfzghPDn";

        private const string USDT_address = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";

        private readonly IWalletClient walletClient;

        private readonly ITransactionClient transactionClient;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly string routerAddress;

        private readonly string myAddress;

        private readonly string privateKey;

        public SunswapV2Router02(IWalletClient walletClient, ITransactionClient transactionClient, string routerAddress, string myAddress, string privateKey)
        {
            this.walletClient = walletClient;
            this.transactionClient = transactionClient;
            this.routerAddress = routerAddress;
            this.myAddress = myAddress;
            this.privateKey = privateKey;
        }

        public async Task<string> SwapUSDDforUSDTAsync(decimal swapInAmount)
        {
            byte[]? contractAddressBytes = Base58Encoder.DecodeFromBase58Check(routerAddress);
            byte[]? myAddressBytes = Base58Encoder.DecodeFromBase58Check(myAddress);

            Wallet.WalletClient? wallet = walletClient.GetProtocol();
            FunctionABI? functionABI = ABITypedRegistry.GetFunctionABI<SwapExactTokensForTokensFunction>();

            try
            {
                SwapExactTokensForTokensFunction swapFn = new SwapExactTokensForTokensFunction()
                {
                    amountIn = (BigInteger)(swapInAmount * (decimal)Math.Pow(10, 18)),
                    amountOutMin = (BigInteger)((swapInAmount * (decimal)0.995) * (decimal)Math.Pow(10, 6)), // 0.5% slippage
                    to = base58toHex(myAddress),
                    path = new[]{ base58toHex(USDD_address), // USDD
                                  base58toHex(USDT_address), // USDT
                                },
                    deadline = (DateTime.Now + TimeSpan.FromMinutes(5)).ToUnixTime()
                };

                string? encodedHex = new FunctionCallEncoder().EncodeRequest(swapFn, functionABI.Sha3Signature);

                TriggerSmartContract trigger = new TriggerSmartContract
                {
                    ContractAddress = ByteString.CopyFrom(contractAddressBytes),
                    OwnerAddress = ByteString.CopyFrom(myAddressBytes),
                    Data = ByteString.CopyFrom(ByteArrary.HexToByteArray(encodedHex))
                };

                TransactionExtention? transactionExtention = await wallet.TriggerConstantContractAsync(trigger, headers: walletClient.GetHeaders());

                if (!transactionExtention.Result.Result)
                {
                    logger.Warn($"[transfer]transfer failed, message={transactionExtention.Result.Message.ToStringUtf8()}.");
                    return null;
                }

                var transaction = transactionExtention.Transaction;
                transaction.RawData.FeeLimit = 100 * 1000000L;

                if (transaction.Ret.Count > 0 && transaction.Ret[0].Ret == Transaction.Types.Result.Types.code.Failed)
                {
                    this.logger.Debug("Swap: Ret failed status");
                    return null;
                }

                Transaction? transSign = transactionClient.GetTransactionSign(transaction, privateKey);

                await transactionClient.BroadcastTransactionAsync(transSign);

                return transSign.GetTxid();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, ex.Message);
                return null;
            }
        }

        private string base58toHex(string base58String)
        {
            NBitcoin.DataEncoders.Base58Encoder base58Encoder = new NBitcoin.DataEncoders.Base58Encoder();
            byte[]? bytes = base58Encoder.DecodeData(base58String);

            HexEncoder hexEncoder = new HexEncoder();

            string hex = hexEncoder.EncodeData(bytes.Skip(1).Take(20).ToArray());
            return hex;
        }

        [Function("swapExactTokensForTokens")]
        public class SwapExactTokensForTokensFunction : FunctionMessage
        {
            [Parameter("uint256", "amountIn", 1)]
            public BigInteger amountIn { get; set; }

            [Parameter("uint256", "amountOutMin", 2)]
            public BigInteger amountOutMin { get; set; }

            [Parameter("address[]", "path", 3)]
            public string[] path { get; set; }

            [Parameter("address", "to", 4)]
            public string to { get; set; }

            [Parameter("uint256", "deadline", 5)]
            public BigInteger deadline { get; set; }
        }

        public async Task<getReservesOutput> GetReservesAsync(string USDD_USDT_LP_PAIR_Address)
        {
            byte[] contractAddressBytes = Base58Encoder.DecodeFromBase58Check(USDD_USDT_LP_PAIR_Address);

            GetReservesFunction getReserves = new GetReservesFunction();

            FunctionCallEncoder callEncoder = new FunctionCallEncoder();
            FunctionABI? functionABI = ABITypedRegistry.GetFunctionABI<GetReservesFunction>();

            string encodedHex = callEncoder.EncodeRequest(getReserves, functionABI.Sha3Signature);

            TriggerSmartContract trigger = new TriggerSmartContract
            {
                ContractAddress = ByteString.CopyFrom(contractAddressBytes),
                Data = ByteString.CopyFrom(encodedHex.HexToByteArray()),
            };

            Wallet.WalletClient? wallet = walletClient.GetProtocol();
            TransactionExtention? txnExt = await wallet.TriggerConstantContractAsync(trigger, headers: walletClient.GetHeaders());

            string result = txnExt.ConstantResult[0].ToByteArray().ToHex();

            getReservesOutput reserves = new FunctionCallDecoder().DecodeOutput<getReservesOutput>(result);

            return reserves;
        }

        [Function("getReserves")]
        public class GetReservesFunction : FunctionMessage
        {
        }

        [FunctionOutput]
        public class getReservesOutput : IFunctionOutputDTO
        {
            [Parameter("uint112", "_reserve0", 1)]
            public virtual BigInteger _reserve0 { get; set; }

            [Parameter("uint112", "_reserve1", 2)]
            public virtual BigInteger _reserve1 { get; set; }

            [Parameter("uint32", "_blockTimestampLast", 3)]
            public virtual BigInteger _blockTimestampLast { get; set; }
        }

        public async Task<string> AddLiquidityAsync(decimal USDD_amount, decimal USDT_amount)
        {
            byte[] contractAddressBytes = Base58Encoder.DecodeFromBase58Check(routerAddress);
            byte[] myAddressBytes = Base58Encoder.DecodeFromBase58Check(myAddress);

            FunctionABI functionABI = ABITypedRegistry.GetFunctionABI<addLiquidityFunction>();

            try
            {
                decimal desiredUSDD = USDD_amount * (decimal) Math.Pow(10, 18);
                decimal desiredUSDT = USDD_amount * (decimal) Math.Pow(10, 6);

                addLiquidityFunction addLiquidityFn = new addLiquidityFunction()
                {
                    tokenA = base58toHex(USDD_address),
                    tokenB = base58toHex(USDT_address),
                    amountADesired = (BigInteger)(desiredUSDD),
                    amountBDesired = (BigInteger)(desiredUSDT),
                    amountAMin = (BigInteger)(desiredUSDD * (decimal)0.995),
                    amountBMin = (BigInteger)(desiredUSDT * (decimal)0.995),
                    to = base58toHex(myAddress),
                    deadline = (DateTime.Now + TimeSpan.FromMinutes(5)).ToUnixTime()
                };

                string encodedHex = new FunctionCallEncoder().EncodeRequest(addLiquidityFn, functionABI.Sha3Signature);

                Transaction transaction = await TronHelpers.TriggerContractAsync(this.walletClient, contractAddressBytes, myAddressBytes, encodedHex);

                transaction.RawData.FeeLimit = 150 * 1000000L;

                if (transaction.Ret.Count > 0 && transaction.Ret[0].Ret == Transaction.Types.Result.Types.code.Failed)
                {
                    this.logger.Debug("AddL: Ret failed status");
                    return null;
                }

                Transaction transSign = transactionClient.GetTransactionSign(transaction, privateKey);

                await transactionClient.BroadcastTransactionAsync(transSign);

                return transSign.GetTxid();
            }
            catch (Exception ex)
            {
                this.logger.Error(ex, ex.Message);
                return null;
            }
        }

        [Function("addLiquidity")]
        public class addLiquidityFunction : FunctionMessage
        {
            [Parameter("address", "tokenA", 1)]
            public string tokenA { get; set; }

            [Parameter("address", "tokenB", 2)]
            public string tokenB { get; set; }

            [Parameter("uint256", "amountADesired", 3)]
            public BigInteger amountADesired { get; set; }

            [Parameter("uint256", "amountBDesired", 4)]
            public BigInteger amountBDesired { get; set; }

            [Parameter("uint256", "amountAMin", 5)]
            public BigInteger amountAMin { get; set; }

            [Parameter("uint256", "amountBMin", 6)]
            public BigInteger amountBMin { get; set; }

            [Parameter("address", "to", 7)]
            public string to { get; set; }

            [Parameter("uint256", "deadline", 8)]
            public BigInteger deadline { get; set; }
        }
    }
}
