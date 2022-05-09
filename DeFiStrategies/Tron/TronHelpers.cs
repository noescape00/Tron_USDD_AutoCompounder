using Google.Protobuf;
using NLog;
using TronNet;
using TronNet.Protocol;

namespace DeFi_Strategies.Tron
{
    public static class TronHelpers
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task<Transaction> TriggerContractAsync(IWalletClient walletClient, byte[] contractAddressBytes, byte[] myAddressBytes, string encodedHexData)
        {
            Wallet.WalletClient? wallet = walletClient.GetProtocol();

            var trigger = new TriggerSmartContract
            {
                ContractAddress = ByteString.CopyFrom(contractAddressBytes),
                OwnerAddress = ByteString.CopyFrom(myAddressBytes),
                Data = ByteString.CopyFrom(encodedHexData.HexToByteArray())
            };


            var transactionExtention = await wallet.TriggerConstantContractAsync(trigger, headers: walletClient.GetHeaders());

            if (!transactionExtention.Result.Result)
            {
                logger.Warn($"[transfer]transfer failed, message={transactionExtention.Result.Message.ToStringUtf8()}.");
                return null;
            }

            Transaction? transaction = transactionExtention.Transaction;

            return transaction;
        }
    }
}
