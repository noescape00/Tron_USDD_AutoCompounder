# USDD autocompounder

This bot does the following: 



1.  Check claimable USDD on SunSwap USDD-USDT LP pool https://sun.io/?lang=en-US#/stake
2. If claimable USDD > threshold => claim USDD
3. Swap half of available USDD for USDT
4. Enter USDD:USDT LP
5. Deposit LP tokens to sunswap



## Prerequisites

Get trongrid api key here: https://www.trongrid.io/

Install .net 6 from here: https://dotnet.microsoft.com/en-us/download/dotnet/6.0



## How to run



1. Clone repository and build project using `dotnet build --configuration Release`
2. Go to `\bin\Release\net6.0` and edit `appsettings.json`: insert your mnemonic and `TronGridAPIKey`, also set `ClaimThresholdUSDD` to any desirable value (compounding will happen when claimable USDD amount is larger than configured threshold)
3. Run `DeFi_Strategies.exe`