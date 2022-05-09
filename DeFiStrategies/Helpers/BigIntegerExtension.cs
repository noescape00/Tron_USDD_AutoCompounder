using System.Numerics;

namespace DeFi_Strategies.Helpers
{
    public static class BigIntegerExtension
    {
        /// <summary>10^18</summary>
        public static BigInteger ETHDivider = BigInteger.Pow(new BigInteger(10), 18);

        public static double DivideToDouble(this BigInteger x, BigInteger y)
        {
            double result = Math.Exp(BigInteger.Log(x) - BigInteger.Log(y));

            return result;
        }

        public static BigInteger MultiplyByDouble(this BigInteger x, double y)
        {
            BigInteger result = BigInteger.Multiply(x, (BigInteger)(y * 100000)) / 100000;

            return result;
        }
    }
}
