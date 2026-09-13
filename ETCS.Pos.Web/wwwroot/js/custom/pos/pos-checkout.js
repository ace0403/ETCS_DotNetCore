/**
 * POS checkout: cashless (iBonus), cash, card, and undo flows.
 */
(function (App) {
    const state = App.state;
    const { apiIsSuccess, apiMessage, getJsonProp, buildLegacyPurchasePayload, makeTransactionId, validateOrderSetup, spendLimitExceeded, spendLimitMessage, parseNumber } = App.helpers;
    const { BridgeClient, PosApiClient } = App.api;

    function normalizeCardSn(value) {
        return String(value || '').replace(/[\s:\-\.]/g, '').toUpperCase();
    }

    function nfcCardMatchesLastSale(bridgeData, lastCardSn) {
        const last = normalizeCardSn(lastCardSn);
        if (!last) return false;
        const candidates = [
            getJsonProp(bridgeData, 'cardSn'),
            getJsonProp(bridgeData, 'uidHex'),
            getJsonProp(bridgeData, 'uidHexReversed'),
            getJsonProp(bridgeData, 'uidDecimal'),
            getJsonProp(bridgeData, 'uidDecimalReversed')
        ];
        return candidates.some(value => normalizeCardSn(value) === last);
    }

    App.checkout = {
        async checkout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Cart is empty.');
                return;
            }
            if (!await validateOrderSetup()) {
                return;
            }

            const lines = App.cart.buildPostPurchaseLines();
            if (lines.length === 0) {
                await App.ui.warning('Cart items are missing item codes required for purchase.');
                return;
            }

            await App.ui.runPosAction('Processing payment', 'Please tap card on the reader…', async () => {
                const meta = App.helpers.getTerminalMeta();
                const payable = App.cart.getPayableAmount();
                const transactionId = makeTransactionId();
                const itemCount = App.cart.cartItemCount();
                const legacy = buildLegacyPurchasePayload();

                if (App.config.prePrintReceipt) {
                    await App.cart.printCurrentReceipt();
                }

                const bridgeResult = await BridgeClient.purchase(payable, transactionId, itemCount);
                if (!bridgeResult.ok || !apiIsSuccess(bridgeResult.data)) {
                    await App.ui.error(apiMessage(bridgeResult.data, 'iBonus purchase failed.'));
                    App.cart.clear(false);
                    return;
                }

                const customerId = getJsonProp(bridgeResult.data, 'customerId') || '';
                if (!customerId) {
                    await App.ui.error('iBonus did not return a customer ID.');
                    App.cart.clear(false);
                    return;
                }

                const spend = await PosApiClient.spendInfo(customerId);
                if (!spend.ok) {
                    await PosApiClient.rollbackSpendLimit({ customerId, amount: payable });
                    await App.ui.error('Unable to resolve student for customer ID.');
                    App.cart.clear(false);
                    return;
                }

                if (spendLimitExceeded(spend.data)) {
                    await PosApiClient.rollbackSpendLimit({ customerId, amount: payable });
                    await App.ui.error(spendLimitMessage(spend.data));
                    App.cart.clear(false);
                    return;
                }

                const postResult = await PosApiClient.postPurchaseLines({
                    customerId,
                    transactionId,
                    ipAddress: meta.ip,
                    branchCode: legacy.branchCode,
                    lines
                });
                if (!postResult.ok || !apiIsSuccess(postResult.data)) {
                    await PosApiClient.rollbackSpendLimit({ customerId, amount: payable });
                    await App.ui.error(apiMessage(postResult.data, 'POS purchase recording failed.'));
                    App.cart.clear(false);
                    return;
                }

                state.lastTransactionId = transactionId;
                state.lastCustomerId = customerId;
                state.lastPayableAmount = payable;
                state.lastItemCount = itemCount;

                await App.ui.success(apiMessage(postResult.data, 'Cashless transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear(false);
            });
        },

        async undoLast() {
            if (state.posBusy) return;
            if (state.cart.length === 0 && !state.lastTransactionId) {
                await App.ui.warning('No cashless transaction to undo.');
                return;
            }

            const confirmed = await App.ui.confirm('Please show card on the terminal to undo.', 'Undo Cashless');
            if (!confirmed) return;

            await App.ui.runPosAction('Undoing cashless transaction', 'Please wait…', async () => {
                const payable = state.cart.length > 0 ? App.cart.getPayableAmount() : state.lastPayableAmount;
                const itemCount = state.cart.length > 0 ? App.cart.cartItemCount() : (state.lastItemCount || 1);
                const transactionId = state.lastTransactionId;
                if (!transactionId) {
                    await App.ui.warning('No cashless transaction to undo.');
                    return;
                }

                const bridgeResult = await BridgeClient.undo(payable, transactionId, itemCount);
                if (!bridgeResult.ok || !apiIsSuccess(bridgeResult.data)) {
                    await App.ui.error(apiMessage(bridgeResult.data, 'iBonus undo failed.'));
                    return;
                }

                await App.cart.dispatchReceiptPrint({
                    isUndo: true,
                    overrides: {
                        items: state.cart.length > 0
                            ? state.cart.map(c => ({ name: c.name, price: c.price, quantity: c.quantity }))
                            : [],
                        total: payable
                    }
                });

                state.lastTransactionId = '';
                state.lastCustomerId = '';
                state.lastPayableAmount = 0;
                state.lastItemCount = 0;
                await App.ui.success('Undo cashless transaction successful.');
                App.cart.clear(false);
            });
        },

        async nfcCheckout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Cart is empty.');
                return;
            }
            if (!await validateOrderSetup({ requireReaderIp: false })) {
                return;
            }

            const lines = App.cart.buildPostPurchaseLines();
            if (lines.length === 0) {
                await App.ui.warning('Cart items are missing item codes required for purchase.');
                return;
            }

            await App.ui.runPosAction('Processing NFC payment', 'Please tap card on the NFC reader…', async () => {
                const meta = App.helpers.getTerminalMeta();
                const payable = App.cart.getPayableAmount();
                const transactionId = makeTransactionId();
                const itemCount = App.cart.cartItemCount();
                const legacy = buildLegacyPurchasePayload();

                if (App.config.prePrintReceipt) {
                    await App.cart.printCurrentReceipt();
                }

                const cardResult = await BridgeClient.waitNfcCard();
                if (!cardResult.ok || !apiIsSuccess(cardResult.data)) {
                    await App.ui.error(apiMessage(cardResult.data, 'NFC card read failed.'));
                    return;
                }

                const cardSn = getJsonProp(cardResult.data, 'cardSn') || '';
                if (!cardSn) {
                    await App.ui.error('NFC reader did not return a card serial.');
                    return;
                }

                const postResult = await PosApiClient.nfcPurchase({
                    cardSn,
                    uidHex: getJsonProp(cardResult.data, 'uidHex') || '',
                    uidHexReversed: getJsonProp(cardResult.data, 'uidHexReversed') || '',
                    uidDecimal: getJsonProp(cardResult.data, 'uidDecimal') || '',
                    uidDecimalReversed: getJsonProp(cardResult.data, 'uidDecimalReversed') || '',
                    transactionId,
                    ipAddress: meta.ip,
                    branchCode: legacy.branchCode,
                    lines
                });
                if (!postResult.ok || !apiIsSuccess(postResult.data)) {
                    await App.ui.error(apiMessage(postResult.data, 'NFC cashless purchase failed.'));
                    return;
                }

                state.lastNfcTransactionId = transactionId;
                state.lastNfcCardSn = getJsonProp(postResult.data, 'cardSn') || cardSn;
                state.lastNfcPayableAmount = payable;
                state.lastNfcItemCount = itemCount;
                state.lastCustomerId = getJsonProp(postResult.data, 'customerId') || '';

                await App.ui.success(apiMessage(postResult.data, 'NFC cashless transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear(false);
            });
        },

        async undoNfcLast() {
            if (state.posBusy) return;
            if (state.cart.length === 0 && !state.lastNfcTransactionId) {
                await App.ui.warning('No NFC cashless transaction to undo.');
                return;
            }

            const confirmed = await App.ui.confirm('Please tap the same card on the NFC reader to undo.', 'Undo NFC');
            if (!confirmed) return;

            await App.ui.runPosAction('Undoing NFC cashless transaction', 'Please tap card on the NFC reader…', async () => {
                const payable = state.cart.length > 0 ? App.cart.getPayableAmount() : state.lastNfcPayableAmount;
                const transactionId = state.lastNfcTransactionId;
                if (!transactionId) {
                    await App.ui.warning('No NFC cashless transaction to undo.');
                    return;
                }

                const cardResult = await BridgeClient.waitNfcCard();
                if (!cardResult.ok || !apiIsSuccess(cardResult.data)) {
                    await App.ui.error(apiMessage(cardResult.data, 'NFC card read failed.'));
                    return;
                }

                if (!nfcCardMatchesLastSale(cardResult.data, state.lastNfcCardSn)) {
                    await App.ui.error('Tapped card does not match the last NFC sale.');
                    return;
                }

                const undoResult = await PosApiClient.nfcUndo({
                    cardSn: getJsonProp(cardResult.data, 'cardSn') || '',
                    uidHex: getJsonProp(cardResult.data, 'uidHex') || '',
                    uidHexReversed: getJsonProp(cardResult.data, 'uidHexReversed') || '',
                    uidDecimal: getJsonProp(cardResult.data, 'uidDecimal') || '',
                    uidDecimalReversed: getJsonProp(cardResult.data, 'uidDecimalReversed') || '',
                    amount: payable,
                    transactionId
                });
                if (!undoResult.ok || !apiIsSuccess(undoResult.data)) {
                    await App.ui.error(apiMessage(undoResult.data, 'NFC cashless undo failed.'));
                    return;
                }

                await App.cart.dispatchReceiptPrint({
                    isUndo: true,
                    overrides: {
                        items: state.cart.length > 0
                            ? state.cart.map(c => ({ name: c.name, price: c.price, quantity: c.quantity }))
                            : [],
                        total: payable
                    }
                });

                state.lastNfcTransactionId = '';
                state.lastNfcCardSn = '';
                state.lastNfcPayableAmount = 0;
                state.lastNfcItemCount = 0;
                await App.ui.success(apiMessage(undoResult.data, 'Undo NFC cashless transaction successful.'));
                App.cart.clear(false);
            });
        },

        async cashCheckout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Please select any item to purchase.');
                return;
            }
            if (!await validateOrderSetup()) {
                return;
            }

            await App.ui.runPosAction('Processing cash payment', 'Please wait…', async () => {
                const legacy = buildLegacyPurchasePayload();
                const payable = App.cart.getPayableAmount();
                const result = await PosApiClient.cashPurchase({
                    customerId: 'CASH',
                    amount: payable,
                    branchCode: legacy.branchCode,
                    terminalCode: legacy.terminalCode,
                    terminalCodeNumeric: legacy.terminalCodeNumeric,
                    transactionId: makeTransactionId(),
                    description: 'Cash Purchase'
                });
                if (!result.ok || !apiIsSuccess(result.data)) {
                    await App.ui.error(apiMessage(result.data, 'Cash transaction failed.'));
                    return;
                }
                await App.ui.success(apiMessage(result.data, 'Cash transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear();
            });
        },

        async undoCashCheckout() {
            if (state.posBusy) return;
            if (!await validateOrderSetup()) {
                return;
            }

            const amount = parseNumber(document.getElementById('txtUndoCashAmount')?.value, 0);
            if (amount <= 0) {
                await App.ui.warning('Enter the amount to refund.');
                return;
            }

            await App.ui.runPosAction('Undoing cash transaction', 'Please wait…', async () => {
                const legacy = buildLegacyPurchasePayload();
                const result = await PosApiClient.undoCashPurchase({
                    customerId: 'CASH',
                    amount,
                    branchCode: legacy.branchCode,
                    terminalCode: legacy.terminalCode,
                    terminalCodeNumeric: legacy.terminalCodeNumeric,
                    transactionId: makeTransactionId(),
                    description: 'Undo Cash Purchase'
                });
                if (!result.ok || !apiIsSuccess(result.data)) {
                    await App.ui.error(apiMessage(result.data, 'Undo cash transaction failed.'));
                    return;
                }
                document.getElementById('txtUndoCashAmount').value = '';
                await App.ui.success('Undo cash transaction successful.');
            });
        },

        async cardCheckout() {
            if (state.posBusy) return;
            if (state.cart.length === 0) {
                await App.ui.warning('Cart is empty.');
                return;
            }
            if (!await validateOrderSetup()) {
                return;
            }

            const creditCardNumber = await App.ui.promptText('Credit / Debit Card', 'Card number');
            if (!creditCardNumber) return;

            await App.ui.runPosAction('Processing card payment', 'Please wait…', async () => {
                const legacy = buildLegacyPurchasePayload();
                const payable = App.cart.getPayableAmount();
                const result = await PosApiClient.cardPurchase({
                    customerId: '',
                    creditCardNumber,
                    amount: payable,
                    branchCode: legacy.branchCode,
                    terminalCode: legacy.terminalCode,
                    terminalCodeNumeric: legacy.terminalCodeNumeric,
                    transactionId: makeTransactionId(),
                    description: 'Card Purchase'
                });
                if (!result.ok || !apiIsSuccess(result.data)) {
                    await App.ui.error(apiMessage(result.data, 'Card purchase failed.'));
                    return;
                }
                await App.ui.success(apiMessage(result.data, 'Card transaction successful.'));
                try {
                    await App.cart.printCurrentReceipt();
                } catch (printError) {
                    console.warn('Receipt print failed:', printError);
                }
                App.cart.clear();
            });
        }
    };
})(window.PosApp);
