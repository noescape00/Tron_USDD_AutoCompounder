# USDD autocompounder

This bot does the following: 

1.  Check claimable USDD on SunSwap USDD-USDT LP pool https://sun.io/?lang=en-US#/stake
2. If claimable USDD > threshold => claim USDD
3. If available USDD > threshold  => swap half of USDD for USDT
4. Enter USDD:USDT liquidity pool
5. Deposit LP tokens to sunswap



## Prerequisites

Get trongrid api key here: https://www.trongrid.io/

Install .net 6 from here: https://dotnet.microsoft.com/en-us/download/dotnet/6.0

Install Microsoft Visual C++ Redistributable from here: https://docs.microsoft.com/en-US/cpp/windows/latest-supported-vc-redist?view=msvc-170

## How to run

1. Clone repository and build project using `dotnet build --configuration Release`
2. Go to `\bin\Release\net6.0` and edit `appsettings.json`: insert your mnemonic and `TronGridAPIKey`, also set `ClaimThresholdUSDD` to any desirable value (compounding will happen when claimable USDD amount is larger than configured threshold)
3. Run `DeFi_Strategies.exe`



Build & setup video here: https://www.youtube.com/watch?v=4C5iuqsIbtg





p.s.

On average it costs 12.5$ in TRX fees to pay for 4 txes to autocompound. 

So probably don't set autocompounding threshold at less than $12.5. 

I'd suggest using smth like $150-200 for a threshold. 



p.s.s

Contact: @Xudox0 (telegram)
