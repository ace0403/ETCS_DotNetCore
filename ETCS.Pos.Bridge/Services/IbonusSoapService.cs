using System;
using System.Net.NetworkInformation;
using AVISoap.AVISoapWebReference;
using ETCS.Pos.Bridge.Models;

namespace ETCS.Pos.Bridge.Services;

public sealed class IbonusSoapService
{
    private const int SoapPort = 18083;

    public IbonusConnectTestResult TestConnection(string terminalIp)
    {
        if (string.IsNullOrWhiteSpace(terminalIp))
        {
            return new IbonusConnectTestResult
            {
                IsReachable = false,
                Message = "Terminal IP is required."
            };
        }

        var host = terminalIp.Trim();
        var soapUrl = BuildSoapUrl(host);

        try
        {
            using var ping = new Ping();
            var pingReply = ping.Send(host, 3000);
            if (pingReply.Status != IPStatus.Success)
            {
                return new IbonusConnectTestResult
                {
                    IsReachable = false,
                    Message = "Network problem, communication with terminal failed.",
                    SoapUrl = soapUrl
                };
            }

            return new IbonusConnectTestResult
            {
                IsReachable = true,
                Message = "Terminal is reachable.",
                SoapUrl = soapUrl
            };
        }
        catch (Exception ex)
        {
            return new IbonusConnectTestResult
            {
                IsReachable = false,
                Message = "Ping failed: " + ex.Message,
                SoapUrl = soapUrl
            };
        }
    }

    public IbonusOperationResult Purchase(IbonusPurchaseRequest request) =>
        Execute(request.TerminalIp, request.Amount, request.TransactionId, request.ItemCount, "5002");

    public IbonusOperationResult Undo(IbonusUndoRequest request) =>
        Execute(request.TerminalIp, request.Amount, request.TransactionId, request.ItemCount, "5003");

    private static IbonusOperationResult Execute(
        string terminalIp,
        decimal amount,
        string transactionId,
        int itemCount,
        string posType)
    {
        if (string.IsNullOrWhiteSpace(terminalIp))
        {
            return Fail("Terminal IP is required.");
        }

        if (amount <= 0)
        {
            return Fail("Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return Fail("Transaction ID is required.");
        }

        try
        {
            using var ping = new Ping();
            var pingReply = ping.Send(terminalIp.Trim(), 3000);
            if (pingReply.Status != IPStatus.Success)
            {
                return Fail("Network problem, communication with terminal failed.");
            }

            var pos = new iBonusPOS
            {
                Url = BuildSoapUrl(terminalIp.Trim())
            };

            var posRequest = new SOAPPOSRequest
            {
                amountCN = Convert.ToUInt32(amount * 100m),
                bonusPoint = 0,
                transactionID = transactionId,
                challenge = string.Empty,
                description = string.Empty,
                dob = 0,
                posType = Convert.ToUInt16(posType),
                quickReloadCN = 0,
                refCode = 0,
                sequenceNo = 1,
                terminalSN = string.Empty
            };

            var posItem = new Items
            {
                itemCount = itemCount > 0 ? itemCount : 1
            };

            var response = pos.posServiceRoutine(posRequest, posItem);
            var result = response.@return;

            if (result.posResult == 9)
            {
                return new IbonusOperationResult
                {
                    IsSuccess = false,
                    PosResult = result.posResult,
                    Message = "Insufficient balance."
                };
            }

            if (result.posResult != 0)
            {
                return new IbonusOperationResult
                {
                    IsSuccess = false,
                    PosResult = result.posResult,
                    Message = "iBonus transaction failed with code " + result.posResult + "."
                };
            }

            return new IbonusOperationResult
            {
                IsSuccess = true,
                PosResult = result.posResult,
                CustomerId = result.customerID ?? string.Empty,
                Message = posType == "5003" ? "Undo successful." : "Purchase successful.",
                BalPrepaidCn = result.balPrepaidCN,
                AccSpendingCn = result.accSpendingCN
            };
        }
        catch (Exception ex)
        {
            return Fail("iBonus communication failed: " + ex.Message);
        }
    }

    private static string BuildSoapUrl(string terminalIp) =>
        "http://" + terminalIp + ":" + SoapPort;

    private static IbonusOperationResult Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
