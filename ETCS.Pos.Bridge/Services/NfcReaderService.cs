using System;
using System.Collections.Generic;
using System.Linq;
using ETCS.Pos.Bridge.Configuration;
using ETCS.Pos.Bridge.Models;
using PCSC;
using PCSC.Exceptions;
using PCSC.Iso7816;

namespace ETCS.Pos.Bridge.Services;

public sealed class NfcReaderService
{
    private const string PreferredPiccReader = "ACS ACR1552 1S CL Reader PICC 0";

    public NfcStatusResult GetStatus()
    {
        try
        {
            using var context = EstablishContext();
            var readers = context.GetReaders() ?? Array.Empty<string>();
            var selected = SelectPiccReader(readers);
            return new NfcStatusResult
            {
                IsReady = selected.Length > 0,
                ReaderName = selected,
                Readers = readers,
                Message = selected.Length == 0
                    ? "NFC reader not found. Connect the ACR1552U and install the ACS PC/SC driver. The reader must be in PC/SC mode, not keyboard emulation."
                    : "NFC reader ready."
            };
        }
        catch (PCSCException ex) when (ex.SCardError == SCardError.NoReadersAvailable)
        {
            return new NfcStatusResult
            {
                IsReady = false,
                Message = "NFC reader not found. Connect the ACR1552U and install the ACS PC/SC driver."
            };
        }
        catch (Exception ex)
        {
            return new NfcStatusResult
            {
                IsReady = false,
                Message = ContextError(ex)
            };
        }
    }

    public NfcWaitCardResult WaitForCard(NfcWaitCardRequest request)
    {
        var timeoutSeconds = request.TimeoutSeconds > 0
            ? Math.Min(request.TimeoutSeconds, 120)
            : BridgeSettings.NfcWaitTimeoutSeconds;

        try
        {
            using var context = EstablishContext();
            var readers = context.GetReaders() ?? Array.Empty<string>();
            var readerName = SelectPiccReader(readers);
            if (readerName.Length == 0)
            {
                return Fail("NFC reader not found. Connect the ACR1552U and install the ACS PC/SC driver. The reader must be in PC/SC mode, not keyboard emulation.");
            }

            if (!WaitUntilPresent(context, readerName, timeoutSeconds, out var waitError))
            {
                return Fail(waitError);
            }

            if (!TryReadUid(context, readerName, out var uid, out var readError))
            {
                return Fail(readError);
            }

            var reversed = NfcUidEncoder.Reverse(uid);
            var uidHex = NfcUidEncoder.ToHex(uid);
            return new NfcWaitCardResult
            {
                IsSuccess = true,
                CardSn = uidHex,
                UidHex = uidHex,
                UidHexReversed = NfcUidEncoder.ToHex(reversed),
                UidDecimal = NfcUidEncoder.ToDecimal(uid),
                UidDecimalReversed = NfcUidEncoder.ToDecimal(reversed),
                ReaderName = readerName,
                Message = "Card read."
            };
        }
        catch (PCSCException ex) when (ex.SCardError == SCardError.NoReadersAvailable)
        {
            return Fail("NFC reader not found. Connect the ACR1552U and install the ACS PC/SC driver.");
        }
        catch (Exception ex)
        {
            return Fail(ContextError(ex));
        }
    }

    private static ISCardContext EstablishContext()
    {
        try
        {
            return ContextFactory.Instance.Establish(SCardScope.System);
        }
        catch (PCSCException)
        {
            return ContextFactory.Instance.Establish(SCardScope.User);
        }
    }

    private static string SelectPiccReader(IReadOnlyList<string> readers)
    {
        var exact = readers.FirstOrDefault(name =>
            string.Equals(name, PreferredPiccReader, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var picc = readers.FirstOrDefault(name =>
            name.IndexOf("ACR1552", StringComparison.OrdinalIgnoreCase) >= 0
            && name.IndexOf("PICC", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!string.IsNullOrWhiteSpace(picc))
        {
            return picc;
        }

        return string.Empty;
    }

    private static bool WaitUntilPresent(ISCardContext context, string readerName, int timeoutSeconds, out string error)
    {
        using var readerState = new SCardReaderState
        {
            ReaderName = readerName,
            CurrentState = SCRState.Unaware
        };
        var states = new[] { readerState };

        var status = context.GetStatusChange(IntPtr.Zero, states);
        if (status == SCardError.Success && IsPresent(readerState.EventState))
        {
            error = string.Empty;
            return true;
        }

        readerState.CurrentState = readerState.EventState;
        status = context.GetStatusChange((IntPtr)(timeoutSeconds * 1000), states);
        if (status == SCardError.Timeout)
        {
            error = "Waiting for card timed out.";
            return false;
        }

        if (status != SCardError.Success)
        {
            error = "Unable to wait for an NFC card (" + status + ").";
            return false;
        }

        if (!IsPresent(readerState.EventState))
        {
            error = "Waiting for card timed out.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadUid(ISCardContext context, string readerName, out byte[] uid, out string error)
    {
        uid = Array.Empty<byte>();
        try
        {
            using var reader = context.ConnectReader(readerName, SCardShareMode.Shared, SCardProtocol.Any);
            var apdu = new CommandApdu(IsoCase.Case2Short, reader.Protocol)
            {
                CLA = 0xFF,
                Instruction = InstructionCode.GetData,
                P1 = 0x00,
                P2 = 0x00,
                Le = 0
            };

            using (reader.Transaction(SCardReaderDisposition.Leave))
            {
                var sendPci = SCardPCI.GetPci(reader.Protocol);
                var receivePci = new SCardPCI();
                var receiveBuffer = new byte[256];
                var command = apdu.ToArray();
                var bytesReceived = reader.Transmit(
                    sendPci,
                    command,
                    command.Length,
                    receivePci,
                    receiveBuffer,
                    receiveBuffer.Length);

                var response = new ResponseApdu(receiveBuffer, bytesReceived, IsoCase.Case2Short, reader.Protocol);
                if (response.SW1 != 0x90 || response.SW2 != 0x00)
                {
                    error = "Failed to read card UID (SW " + response.SW1.ToString("X2") + response.SW2.ToString("X2") + ").";
                    return false;
                }

                if (!response.HasData)
                {
                    error = "Failed to read card UID.";
                    return false;
                }

                uid = response.GetData();
                error = string.Empty;
                return true;
            }
        }
        catch (PCSCException ex) when (ex.SCardError == SCardError.NoSmartcard)
        {
            error = "No card on the NFC reader.";
            return false;
        }
        catch (PCSCException ex)
        {
            error = "Unable to connect to the NFC reader (" + ex.SCardError + ").";
            return false;
        }
        catch (Exception ex)
        {
            error = "Failed to read card UID: " + ex.Message;
            return false;
        }
    }

    private static bool IsPresent(SCRState eventState) =>
        eventState.HasFlag(SCRState.Present) && !eventState.HasFlag(SCRState.Empty);

    private static string ContextError(Exception ex) =>
        "Unable to open the PC/SC resource manager. Install the ACS PC/SC driver and keep the ACR1552U in reader/writer mode. " + ex.Message;

    private static NfcWaitCardResult Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
