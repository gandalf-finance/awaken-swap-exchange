using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Types;
using Awaken.Contracts.Swap;
using Awaken.Contracts.SwapExchangeContract;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Awaken.Contracts.SwapExchange
{
    public class SwapExchangeContractTests : SwapExchangeContractTestBase
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public SwapExchangeContractTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Test()
        {
            await Initialize();
        }

        [Fact]
        public async Task Set_Tests()
        {
            await Initialize();
            var ownerSwapExchangeStub = GetSwapExchangeContractStub(OwnerPair);
            await ownerSwapExchangeStub.SetSwapToTargetTokenThreshold.SendAsync(new Thresholdinput
            {
                CommonTokenThreshold = 500,
                LpTokenThreshold = 300
            });

            await ownerSwapExchangeStub.SetSwapToTargetTokenThreshold.SendAsync(new Thresholdinput
            {
                CommonTokenThreshold = 400,
            });

            var thresholdCallAsync = await ownerSwapExchangeStub.Threshold.CallAsync(new Empty());
            thresholdCallAsync.CommonTokenThreshold.ShouldBe(400);
            thresholdCallAsync.LpTokenThreshold.ShouldBe(300);

            var targetToken = await ownerSwapExchangeStub.TargetToken.CallAsync(new Empty());
            targetToken.Value.ShouldBe(SymbolUsdt);
            await ownerSwapExchangeStub.SetTargetToken.SendAsync(new StringValue
            {
                Value = SymbolElff
            });
            targetToken = await ownerSwapExchangeStub.TargetToken.CallAsync(new Empty());
            targetToken.Value.ShouldBe(SymbolElff);

            var receivor = await ownerSwapExchangeStub.Receivor.CallAsync(new Empty());
            receivor.ShouldBe(receivor);
            await ownerSwapExchangeStub.SetReceivor.SendAsync(Tom);
            receivor = await ownerSwapExchangeStub.Receivor.CallAsync(new Empty());
            receivor.ShouldBe(Tom);
        }

        [Fact]
        public async Task Swap_Common_Token_Path_By_Common_Token_Test()
        {
            await Initialize();
            var ownerSwapExchangeContractStub = GetSwapExchangeContractStub(OwnerPair);
            await ownerSwapExchangeContractStub.SetSwapToTargetTokenThreshold.SendAsync(new Thresholdinput
            {
                CommonTokenThreshold = 500
            });
            var ownerSwapContractStub = GetSwapContractStub(OwnerPair);
            var receiverCommonStub = GetCommonTokenContractStub(ReceiverPair);
            var ownerCommonTokenStub = GetCommonTokenContractStub(OwnerPair);
            var path = new Dictionary<string, Path>();
            var swapTokenList = new TokenList();

            {
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolAave, SymbolLink, SymbolUsdt},
                    AmountIn = 10_00000000
                });
                var usdtOut = expect.Amount[2];
                usdtOut.ShouldBe(796065804L);
                var expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(10_00000000);
                expectPrice.ShouldBe(796065804000000000);
                path[SymbolAave] = new Path
                {
                    Value = {SymbolAave, SymbolLink, SymbolUsdt},
                    ExpectPrice = expectPrice,
                    SlipPoint = 5
                };
            }

            {
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolElff, SymbolLink, SymbolUsdt},
                    AmountIn = 40_000000000
                });
                var usdtOut = expect.Amount[2];
                usdtOut.ShouldBe(1424894545L);
                var expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(40_000000000);
                expectPrice.ShouldBe(35622363625000000);
                path[SymbolElff] = new Path
                {
                    Value = {SymbolElff, SymbolLink, SymbolUsdt},
                    ExpectPrice = expectPrice,
                    SlipPoint = 5
                };
            }

            {
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolLink, SymbolUsdt},
                    AmountIn = 20_00000000
                });
                var usdtOut = expect.Amount.Last();
                usdtOut.ShouldBe(831248957L);
                var expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(20_00000000);
                expectPrice.ShouldBe(415624478500000000);
                path[SymbolLink] = new Path
                {
                    Value = {SymbolLink, SymbolUsdt},
                    ExpectPrice = expectPrice,
                    SlipPoint = 5
                };
            }

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolAave,
                Amount = 10_00000000
            });

            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 10_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolAave
            });


            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolLink,
                Amount = 20_00000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 20_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolLink
            });


            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolElff,
                Amount = 40_000000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 40_000000000,
                Spender = DAppContractAddress,
                Symbol = SymbolElff
            });

            var balanceReceiverBefore = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverBefore.Balance.ShouldBe(0);

            var receipt = await ownerSwapExchangeContractStub.SwapCommonTokens.SendAsync(new SwapTokensInput
            {
                PathMap = {path},
                SwapTokenList = swapTokenList
            });
            var logs = receipt.TransactionResult.Logs;
            foreach (var logEvent in logs)
            {
                var @equals = logEvent.Name.Equals("SwapResultEvent");
                if (equals)
                {
                    var deserializeAElfEvent = DeserializeAElfEvent<SwapResultEvent>(logEvent);
                    _testOutputHelper.WriteLine(deserializeAElfEvent.ToString());
                    if (deserializeAElfEvent.Symbol.Equals("AAVE"))
                    {
                        deserializeAElfEvent.Result.ShouldBe(true);
                        deserializeAElfEvent.IsLptoken.ShouldBe(false);
                        deserializeAElfEvent.Amount.ShouldBe(1000000000);
                    }
                    else
                    {
                        deserializeAElfEvent.Result.ShouldBe(false);
                        deserializeAElfEvent.IsLptoken.ShouldBe(false);
                    }
                }
            }

            var balanceReceiverAfter = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverAfter.Balance.ShouldBeGreaterThan(0);
            balanceReceiverAfter.Balance.ShouldBe(796065804L);
        }

        [Fact]
        public async Task Swap_Common_Token_Path_By_Pair_Test()
        {
            await Initialize();
            var ownerSwapExchangeContractStub = GetSwapExchangeContractStub(OwnerPair);
            await ownerSwapExchangeContractStub.SetSwapToTargetTokenThreshold.SendAsync(new Thresholdinput
            {
                CommonTokenThreshold = 500
            });
            var ownerSwapContractStub = GetSwapContractStub(OwnerPair);
            var receiverCommonStub = GetCommonTokenContractStub(ReceiverPair);
            var ownerCommonTokenStub = GetCommonTokenContractStub(OwnerPair);
            var path = new Dictionary<string, Path>();
            var swapTokenList = new TokenList();
            {
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolAave, SymbolLink, SymbolUsdt},
                    AmountIn = 10_00000000
                });
                var usdtOut = expect.Amount[2];
                usdtOut.ShouldBe(796065804L);
                var expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(10_00000000);
                expectPrice.ShouldBe(796065804000000000);
                path[SymbolAave] = new Path
                {
                    Value = {$"ALP {SymbolAave}-{SymbolLink}", $"{SymbolLink}-{SymbolUsdt}"},
                    ExpectPrice = expectPrice,
                    SlipPoint = 5
                };
            }

            {
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolElff, SymbolLink, SymbolUsdt},
                    AmountIn = 40_000000000
                });
                var usdtOut = expect.Amount[2];
                usdtOut.ShouldBe(1424894545L);
                var expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(40_000000000);
                expectPrice.ShouldBe(35622363625000000);
                path[SymbolElff] = new Path
                {
                    Value = {$"ALP {SymbolElff}-{SymbolLink}", $"{SymbolLink}-{SymbolUsdt}"},
                    ExpectPrice = expectPrice,
                    SlipPoint = 5
                };
            }

            {
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolLink, SymbolUsdt},
                    AmountIn = 20_00000000
                });
                var usdtOut = expect.Amount.Last();
                usdtOut.ShouldBe(831248957L);
                var expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(20_00000000);
                expectPrice.ShouldBe(415624478500000000);
                path[SymbolLink] = new Path
                {
                    Value = {$"{SymbolLink}-{SymbolUsdt}"},
                    ExpectPrice = expectPrice,
                    SlipPoint = 5
                };
            }

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolAave,
                Amount = 10_00000000
            });

            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 10_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolAave
            });


            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolLink,
                Amount = 20_00000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 20_00000000,
                Spender = DAppContractAddress,
                Symbol = SymbolLink
            });


            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = SymbolElff,
                Amount = 40_000000000
            });
            await ownerCommonTokenStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 40_000000000,
                Spender = DAppContractAddress,
                Symbol = SymbolElff
            });

            var balanceReceiverBefore = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverBefore.Balance.ShouldBe(0);

            var receipt = await ownerSwapExchangeContractStub.SwapCommonTokens.SendAsync(new SwapTokensInput
            {
                PathMap = {path},
                SwapTokenList = swapTokenList
            });


            var balanceReceiverAfter = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverAfter.Balance.ShouldBeGreaterThan(0);
            balanceReceiverAfter.Balance.ShouldBe(796065804L);
        }

        [Fact]
         public async Task Swap_LpToken_Test()
        {
            await Initialize();
            var ownerSwapExchangeContractStub = GetSwapExchangeContractStub(OwnerPair);
            await ownerSwapExchangeContractStub.SetSwapToTargetTokenThreshold.SendAsync(new Thresholdinput
            {
                LpTokenThreshold = 500
            });
            var receiverCommonStub = GetCommonTokenContractStub(ReceiverPair);
            var ownerCommonTokenContractStub = GetCommonTokenContractStub(OwnerPair);

            var ownerLpTokenStub = GetLpTokenContractStub(OwnerPair);
            var ownerSwapContractStub = GetSwapContractStub(OwnerPair);
            var path = new Dictionary<string, Path>();
            var swapTokenList = new TokenList();

            var lp1 = GetTokenPairSymbol(SymbolLink, SymbolElff);
            var lp2 = GetTokenPairSymbol(SymbolLink, SymbolUsdt);
            var lp3 = GetTokenPairSymbol(SymbolAave, SymbolLink);
            var expectPrice = new BigIntValue(0);
            {
                var swapInAmount = 20_00000000;
                var expect = await ownerSwapContractStub.GetAmountsOut.CallAsync(new GetAmountsOutInput
                {
                    Path = {SymbolElff, SymbolLink, SymbolUsdt},
                    AmountIn = swapInAmount
                });
                var usdtOut = expect.Amount.Last();
                expectPrice = new BigIntValue(ExpansionCoefficient).Mul(usdtOut).Div(swapInAmount);
                expectPrice.ShouldBe(191332058000000000);
            }

            path[SymbolAave] = new Path
            {
                Value = {$"ALP {SymbolAave}-{SymbolLink}", $"ALP {SymbolLink}-{SymbolUsdt}"},
                ExpectPrice = expectPrice,
                SlipPoint = 5
            };

            path[SymbolElff] = new Path
            {
                Value = {$"{SymbolElff}-{SymbolLink}", $"{SymbolLink}-{SymbolUsdt}"},
                ExpectPrice = expectPrice,
                SlipPoint = 5
            };

            path[SymbolLink] = new Path
            {
                Value = {$"{SymbolLink}-{SymbolUsdt}"},
                ExpectPrice = expectPrice,
                SlipPoint = 5
            };

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = lp1,
                Amount = 20_00000000
            });
            await ownerLpTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 20_00000000,
                Spender = DAppContractAddress,
                Symbol = lp1
            });

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                Amount = 10_00000000,
                TokenSymbol = lp2
            });

            await ownerLpTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 10_00000000,
                Spender = DAppContractAddress,
                Symbol = lp2
            });

            swapTokenList.TokensInfo.Add(new SwapExchangeContract.Token
            {
                TokenSymbol = lp3,
                Amount = 100_00000000
            });

            await ownerLpTokenStub.Approve.SendAsync(new Token.ApproveInput
            {
                Amount = 100_00000000,
                Spender = DAppContractAddress,
                Symbol = lp3
            });

            var balanceReceiverBefore = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverBefore.Balance.ShouldBe(0);

            var ownerAaveBalanceBefore = await ownerCommonTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Owner,
                Symbol = "AAVE"
            });

            var ownerElffBalanceBefore = await ownerCommonTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Owner,
                Symbol = "ELFF"
            });

            var ownerLinkBalanceBefore = await ownerCommonTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Owner,
                Symbol = "LINK"
            });


            var executionResult = await ownerSwapExchangeContractStub.SwapLpTokens.SendAsync(new SwapTokensInput
            {
                PathMap = {path},
                SwapTokenList = swapTokenList
            });

            var logs = executionResult.TransactionResult.Logs;
            foreach (var logEvent in logs)
            {
                var @equals = logEvent.Name.Equals("SwapResultEvent");
                if (equals)
                {
                    var deserializeAElfEvent = DeserializeAElfEvent<SwapResultEvent>(logEvent);
                    _testOutputHelper.WriteLine(deserializeAElfEvent.ToString());
                    if (deserializeAElfEvent.Symbol.Equals("AAVE"))
                    {
                        deserializeAElfEvent.Result.ShouldBe(true);
                        deserializeAElfEvent.Amount.ShouldBe(7071067811);
                        
                        var ownerAaveBalanceAfter = await ownerCommonTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                        {
                            Owner = Owner,
                            Symbol = "AAVE"
                        });
                        ownerAaveBalanceAfter.Balance.ShouldBe(ownerAaveBalanceBefore.Balance);
                    }
                    else
                    {
                        if (deserializeAElfEvent.Symbol.Equals("ELFF"))
                        {
                            var ownerElffBalanceAfter = await ownerCommonTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                            {
                                Owner = Owner,
                                Symbol = "ELFF"
                            });
                            ownerElffBalanceAfter.Balance.ShouldBe(ownerAaveBalanceBefore.Balance.Add(deserializeAElfEvent.Amount));
                            deserializeAElfEvent.IsLptoken.ShouldBe(false);
                            deserializeAElfEvent.Result.ShouldBe(false);
                        }

                        if (deserializeAElfEvent.Symbol.Equals("LINK"))
                        {
                            var ownerLinkBalanceAfter = await ownerCommonTokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                            {
                                Owner = Owner,
                                Symbol = "LINK"
                            });
                            ownerLinkBalanceAfter.Balance.ShouldBe(
                                ownerLinkBalanceBefore.Balance.Add(deserializeAElfEvent.Amount));
                            deserializeAElfEvent.IsLptoken.ShouldBe(false);
                            deserializeAElfEvent.Result.ShouldBe(false);
                        }
                    }
                }
            }

            var balanceReceiverAfter = await receiverCommonStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = Receiver,
                Symbol = SymbolUsdt
            });
            balanceReceiverAfter.Balance.ShouldBeGreaterThan(0);
            balanceReceiverAfter.Balance.ShouldBe(2915609314L);
        }
    }
}